using CES.Business.Extensions.Entities;
using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Entities.Infrastructure;
using CES.Entities.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CES.Business.Services
{
    public class SubmissionService : ISubmissionService
    {
        private readonly ICESDataStore _datastore;
        private readonly IFileStorage _fileStorage;

        public SubmissionService(ICESDataStore dataStore, IFileStorage fileStorage)
        {
            _datastore = dataStore;
            _fileStorage = fileStorage;
        }

        public async Task<bool> SubmitEvidence(EvidenceSubmissionModel model)
        {
            if (model.Tickets == null || model.Tickets.Count == 0)
                return false;

            var entity = model.ToEntity();
            await _datastore.Submissions.AddAsync(entity);

            // Flush now so entity.Id is populated; files are stored under the submission ID.
            await _datastore.SaveChangesAsync();

            var storagePath = Path.Combine(model.LocationId, model.ShortDate, model.RoomCode, entity.Id.ToString());

            foreach (var file in model.fileUploads)
            {
                var newFile = await _fileStorage.SaveAsync(file, storagePath);
                entity.Files.Add(newFile);
                await _datastore.StoredFiles.AddAsync(newFile);
            }

            await _datastore.SaveChangesAsync();
            return true;
        }

        public async Task<SubmissionReviewModel?> RetrieveSubmission(int submissionId)
        {
            var entity = await _datastore.Submissions
                .Include(s => s.Tickets)
                .Include(s => s.Files.Where(f => !f.IsDeleted))
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (entity == null)
                return null;

            return entity.ToReviewModel();
        }

        public async Task<List<SubmissionReviewModel>> RetrieveSubmissionListing()
        {
            var submissions = await _datastore.Submissions
                .Where(s => !s.IsDeleted)
                .Include(s => s.Tickets)
                .ToListAsync();

            return submissions.Select(s => s.ToReviewModel()).ToList();
        }

        public async Task<bool> AcceptSubmissions(EvidenceAcceptanceModel model)
        {
            var submission = await _datastore.Submissions
                .Include(s => s.Files.Where(f => !f.IsDeleted))
                .FirstOrDefaultAsync(s => s.Id == model.FileId);

            if (submission == null)
                return false;

            int processedCount = 0;
            foreach (var file in model.acceptedFiles)
            {
                var storedFile = await _datastore.StoredFiles.FirstOrDefaultAsync(f => f.Id == file);
                if (storedFile == null)
                    return false;

                await _fileStorage.AcceptAsync(storedFile);
                storedFile.IsDeleted = true;
                processedCount++;
            }

            if (processedCount == submission.Files.Count())
                submission.IsDeleted = true;

            await _datastore.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RejectSubmissions(EvidenceAcceptanceModel model)
        {
            var submission = await _datastore.Submissions
                .Include(s => s.Files.Where(f => !f.IsDeleted))
                .FirstOrDefaultAsync(s => s.Id == model.FileId);

            if (submission == null)
                return false;

            foreach (var file in submission.Files)
            {
                await _fileStorage.DeleteAsync(file);
                file.IsDeleted = true;
            }

            submission.IsDeleted = true;
            await _datastore.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveFileAsync(Guid fileId)
        {
            var file = await _datastore.StoredFiles.FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);
            if (file == null)
                return false;

            if (file.EnteredValue != null)
                throw new InvalidOperationException("Entered exhibits cannot be removed.");

            await _fileStorage.DeleteAsync(file);
            file.IsDeleted = true;
            file.SetUpdateBy("Admin");
            await _datastore.SaveChangesAsync();
            return true;
        }

        public async Task<List<PriorSubmissionModel>> GetSubmissionsByFileNumberAsync(string fileNumberText)
        {
            var submissions = await _datastore.Submissions
                .Where(s => !s.IsDeleted && s.Tickets.Any(t => t.FileNumberText == fileNumberText))
                .Include(s => s.Tickets)
                // .Include(s => s.Files.Where(f => !f.IsDeleted))
                .Include(s => s.Files) 
                .OrderByDescending(s => s.UploadDate)
                .ToListAsync();

            return submissions.Select(s =>
            {
                var matchingTicket = s.Tickets.FirstOrDefault(t => t.FileNumberText == fileNumberText);
                return new PriorSubmissionModel
                {
                    SubmissionId = s.Id,
                    SubmissionDate = s.UploadDate,
                    AppearanceDateTime = matchingTicket?.AppearanceDateTime,
                    Location = s.LocationNameText ?? "",
                    Room = s.RoomText ?? "",
                    Files = s.Files.Select(f => new SubmissionFile
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
                    }).ToList()
                };
            }).ToList();
        }
    }
}
