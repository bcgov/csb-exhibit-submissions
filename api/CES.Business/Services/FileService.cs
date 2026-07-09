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
        private readonly IFileStorage _fileStorage;

        public FileService(ICESDataStore dataStore, IFileStorage fileStorage)
        {
            _dataStore = dataStore;
            _fileStorage = fileStorage;
        }

        public async Task<StoredFiles?> RetrieveFileMetaData(Guid fileId)
        {
            return await _dataStore.StoredFiles.FindAsync(fileId);
        }

        public async Task<(Stream? stream, string? fileName, string? contentType, string? error)> GetExhibitContentAsync(Guid fileId)
        {
            var file = await _dataStore.StoredFiles.FirstOrDefaultAsync(f => f.Id == fileId);

            if (file == null || file.IsDeleted)
                return (null, null, null, "Exhibit not found.");

            try
            {
                // Accepted files come from the canonical store (resolved via the
                // DB-stored path, never a client-supplied one); pending files from
                // the temporary store.
                var stream = file.IsAccepted
                    ? await _fileStorage.GetAcceptedExhibitAsync(file)
                    : await _fileStorage.GetAsync(file);

                return (stream, file.OriginalFileName, file.ContentType, null);
            }
            catch (FileNotFoundException)
            {
                return (null, null, null, "Exhibit file not found.");
            }
        }

        public async Task<SubmissionFile> MarkExhibitAsync(Guid fileId, string markedValue, string changedBy, bool isAdminOverride = false)
        {
            var file = await LoadFileWithSubmissionAsync(fileId)
                ?? throw new KeyNotFoundException($"File {fileId} not found.");

            if (!isAdminOverride && file.EnteredValue != null)
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

            // Per-file auto-accept on first Marked (Decision #13).
            await FinalizeClassificationAsync(file, autoAccept: true);
            return ToSubmissionFile(file);
        }

        public async Task<SubmissionFile> EnterExhibitAsync(Guid fileId, string enteredValue, string changedBy, bool isAdminOverride = false)
        {
            var file = await LoadFileWithSubmissionAsync(fileId)
                ?? throw new KeyNotFoundException($"File {fileId} not found.");

            if (!int.TryParse(enteredValue, out var numericValue)
                || numericValue < ClassificationConstants.EnteredMin
                || numericValue > ClassificationConstants.EnteredMax)
            {
                throw new ArgumentException($"Entered value must be a number between {ClassificationConstants.EnteredMin} and {ClassificationConstants.EnteredMax}.");
            }

            if (!isAdminOverride && file.EnteredAt.HasValue)
            {
                var secondsSinceEntered = (SystemDate.UtcNow() - file.EnteredAt.Value).TotalSeconds;
                if (secondsSinceEntered > ClassificationConstants.ClassificationEditWindowSeconds)
                    throw new InvalidOperationException("Entered exhibits cannot be modified.");
            }

            var oldValue = file.EnteredValue;
            file.EnteredValue = enteredValue;

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

            // Per-file auto-accept on first Entered (Decision #13); if already
            // accepted (Marked→Entered) the bytes/sha stay, only metadata refreshes.
            await FinalizeClassificationAsync(file, autoAccept: true);
            return ToSubmissionFile(file);
        }

        public async Task<SubmissionFile> UpdateExhibitDescriptionAsync(Guid fileId, string description, string changedBy, bool isAdminOverride = false)
        {
            var file = await LoadFileWithSubmissionAsync(fileId)
                ?? throw new KeyNotFoundException($"File {fileId} not found.");

            if (!isAdminOverride && file.EnteredValue != null)
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

            // A description edit never triggers acceptance on its own; it only
            // refreshes the sidecar when the file is already accepted (and, per the
            // guard above, not yet Entered).
            await FinalizeClassificationAsync(file, autoAccept: false);
            return ToSubmissionFile(file);
        }

        // Loads a file together with its submission's tickets and files so promotion
        // and metadata refresh have the full context they need.
        private async Task<StoredFiles?> LoadFileWithSubmissionAsync(Guid fileId)
            => await _dataStore.StoredFiles
                .Include(f => f.Submission).ThenInclude(s => s.Tickets)
                .Include(f => f.Submission).ThenInclude(s => s.Files)
                .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

        // Persists the DB change (source of truth) first, then promotes bytes /
        // (re)writes the sidecar. If a file operation fails the DB stays authoritative
        // and the sidecar is regenerable on the next edit.
        private async Task FinalizeClassificationAsync(StoredFiles file, bool autoAccept)
        {
            if (autoAccept && !file.IsAccepted)
            {
                var result = await _fileStorage.PromoteToAcceptedAsync(file.Submission, file);
                file.IsAccepted = true;
                file.AcceptedAtUTC = SystemDate.UtcNow();
                file.CanonicalPath = result.CanonicalPath;
                file.Sha256 = result.Sha256;
                file.AcceptedFileName = result.AcceptedFileName;
                file.Submission.RecalculateStatus();
            }

            await _dataStore.SaveChangesAsync();

            if (file.IsAccepted)
            {
                var auditLogs = await _dataStore.SubmissionAuditLogs
                    .Where(l => l.SubmissionId == file.SubmissionId)
                    .ToListAsync();
                await _fileStorage.WriteMetadataAsync(file.Submission, auditLogs);
            }
        }

        public async Task<List<ExhibitHistoryEntryModel>> GetExhibitHistoryAsync(Guid fileId)
        {
            var fileExists = await _dataStore.StoredFiles.AnyAsync(f => f.Id == fileId);
            if (!fileExists)
                throw new KeyNotFoundException($"File {fileId} not found.");

            return await _dataStore.SubmissionAuditLogs
                .Where(l => l.FileId == fileId)
                .OrderBy(l => l.ChangedAtUTC)
                .Select(l => new ExhibitHistoryEntryModel
                {
                    FieldName = l.FieldName,
                    OldValue = l.OldValue,
                    NewValue = l.NewValue,
                    ChangedBy = l.ChangedBy,
                    ChangedAtUTC = l.ChangedAtUTC,
                })
                .ToListAsync();
        }

        public async Task<List<ExhibitNoteModel>> GetExhibitNotesAsync(Guid fileId)
        {
            var fileExists = await _dataStore.StoredFiles.AnyAsync(f => f.Id == fileId);
            if (!fileExists)
                throw new KeyNotFoundException($"File {fileId} not found.");

            return await _dataStore.ExhibitNotes
                .Where(n => n.FileId == fileId)
                .OrderBy(n => n.CreatedAtUTC)
                .Select(n => new ExhibitNoteModel
                {
                    Id = n.Id,
                    NoteText = n.NoteText,
                    CreatedBy = n.CreatedBy,
                    CreatedAtUTC = n.CreatedAtUTC,
                })
                .ToListAsync();
        }

        public async Task<ExhibitNoteModel> AddExhibitNoteAsync(Guid fileId, string noteText, string createdBy)
        {
            var fileExists = await _dataStore.StoredFiles.AnyAsync(f => f.Id == fileId);
            if (!fileExists)
                throw new KeyNotFoundException($"File {fileId} not found.");

            var trimmed = (noteText ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                throw new ArgumentException("Note text is required.");
            if (trimmed.Length > ExhibitNoteConstants.NoteMaxLength)
                throw new ArgumentException($"Note cannot exceed {ExhibitNoteConstants.NoteMaxLength} characters.");

            var note = new ExhibitNote
            {
                FileId = fileId,
                NoteText = trimmed,
                CreatedBy = createdBy,
                CreatedAtUTC = SystemDate.UtcNow(),
            };

            _dataStore.ExhibitNotes.Add(note);
            await _dataStore.SaveChangesAsync();

            return new ExhibitNoteModel
            {
                Id = note.Id,
                NoteText = note.NoteText,
                CreatedBy = note.CreatedBy,
                CreatedAtUTC = note.CreatedAtUTC,
            };
        }

        private static SubmissionFile ToSubmissionFile(StoredFiles f) => f.ToSubmissionFile();
    }
}
