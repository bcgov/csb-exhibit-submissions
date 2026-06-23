using CES.Business.Constants;
using CES.Business.Extensions.Entities;
using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Entities;
using CES.Entities.Infrastructure;
using CES.Entities.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CES.Business.Services
{
    public class FileService : IFileService
    {
        private readonly ICESDataStore _dataStore;

        public FileService(ICESDataStore dataStore)
        {
            _dataStore = dataStore;
        }

        public async Task<StoredFiles?> RetrieveFileMetaData(Guid fileId)
        {
            return await _dataStore.StoredFiles.FindAsync(fileId);
        }

        public async Task<SubmissionFile> MarkExhibitAsync(Guid fileId, string markedValue, string changedBy)
        {
            var file = await _dataStore.StoredFiles.FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted)
                ?? throw new KeyNotFoundException($"File {fileId} not found.");

            if (file.EnteredValue != null)
                throw new InvalidOperationException("Entered exhibits cannot be modified.");

            var normalised = markedValue.ToUpperInvariant();
            if (normalised.Length != 1 || normalised[0] < 'A' || normalised[0] > 'Z')
                throw new ArgumentException("Marked value must be a single letter A–Z.");

            var oldValue = file.MarkedValue;
            file.MarkedValue = normalised;
            file.MarkedAt = SystemDate.UtcNow();
            file.SetUpdateBy(changedBy);

            _dataStore.SubmissionAuditLogs.Add(new SubmissionAuditLog
            {
                SubmissionId = file.SubmissionId,
                FileId = file.Id,
                FieldName = "MarkedValue",
                OldValue = oldValue,
                NewValue = normalised,
                ChangedBy = changedBy,
            });

            await _dataStore.SaveChangesAsync();
            return ToSubmissionFile(file);
        }

        public async Task<SubmissionFile> EnterExhibitAsync(Guid fileId, string enteredValue, string changedBy)
        {
            var file = await _dataStore.StoredFiles.FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted)
                ?? throw new KeyNotFoundException($"File {fileId} not found.");

            if (!int.TryParse(enteredValue, out var numericValue)
                || numericValue < ClassificationConstants.EnteredMin
                || numericValue > ClassificationConstants.EnteredMax)
            {
                throw new ArgumentException($"Entered value must be a number between {ClassificationConstants.EnteredMin} and {ClassificationConstants.EnteredMax}.");
            }

            // Terminal lock: reject re-enter if past the correction window
            if (file.EnteredAt.HasValue)
            {
                var secondsSinceEntered = (SystemDate.UtcNow() - file.EnteredAt.Value).TotalSeconds;
                if (secondsSinceEntered > ClassificationConstants.ClassificationEditWindowSeconds)
                    throw new InvalidOperationException("Entered exhibits cannot be modified.");
            }

            var oldValue = file.EnteredValue;
            file.EnteredValue = enteredValue;

            // EnteredAt is only set on first enter; corrections do not advance the timestamp
            if (!file.EnteredAt.HasValue)
                file.EnteredAt = SystemDate.UtcNow();

            file.SetUpdateBy(changedBy);

            _dataStore.SubmissionAuditLogs.Add(new SubmissionAuditLog
            {
                SubmissionId = file.SubmissionId,
                FileId = file.Id,
                FieldName = "EnteredValue",
                OldValue = oldValue,
                NewValue = enteredValue,
                ChangedBy = changedBy,
            });

            await _dataStore.SaveChangesAsync();
            return ToSubmissionFile(file);
        }

        public async Task<SubmissionFile> UpdateExhibitDescriptionAsync(Guid fileId, string description, string changedBy)
        {
            var file = await _dataStore.StoredFiles.FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted)
                ?? throw new KeyNotFoundException($"File {fileId} not found.");

            if (file.EnteredValue != null)
                throw new InvalidOperationException("Entered exhibits cannot be modified.");

            if (description.Length > ClassificationConstants.DescriptionMaxLength)
                throw new ArgumentException($"Description cannot exceed {ClassificationConstants.DescriptionMaxLength} characters.");

            var oldValue = file.Description;
            file.Description = description;
            file.SetUpdateBy(changedBy);

            _dataStore.SubmissionAuditLogs.Add(new SubmissionAuditLog
            {
                SubmissionId = file.SubmissionId,
                FileId = file.Id,
                FieldName = "Description",
                OldValue = oldValue,
                NewValue = description,
                ChangedBy = changedBy,
            });

            await _dataStore.SaveChangesAsync();
            return ToSubmissionFile(file);
        }

        private static SubmissionFile ToSubmissionFile(StoredFiles f) => new()
        {
            Id = f.Id,
            OriginalFileName = f.OriginalFileName,
            StoredFileName = f.StoredFileName,
            ContentType = f.ContentType,
            FileSize = f.FileSize,
            StorageProvider = f.StorageProvider,
            Url = "",
            Status = f.DeriveStatus(),
            MarkedValue = f.MarkedValue,
            MarkedAt = f.MarkedAt,
            EnteredValue = f.EnteredValue,
            EnteredAt = f.EnteredAt,
            Description = f.Description,
        };
    }
}
