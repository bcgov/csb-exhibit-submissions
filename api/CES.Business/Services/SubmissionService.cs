using CES.Business.Extensions.Entities;
using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Entities.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CES.Business.Services
{
    public class SubmissionService : ISubmissionService
    {
        private ICESDataStore _datastore;
        private readonly IFileStorage _fileStorage;
        public SubmissionService (ICESDataStore dataStore, IFileStorage fileStorage)
        {
            _datastore = dataStore;
            _fileStorage = fileStorage;
            
        }
        public async Task<bool> SubmitEvidence(EvidenceSubmissionModel model)
        {

            var entity = model.ToEntity();
            await _datastore.Submissions.AddAsync(entity);

            foreach (var file in model.fileUploads)
            {
                var newFile = await _fileStorage.SaveAsync(file, entity.TicketNumber);
                entity.Files.Add(newFile);
                await _datastore.StoredFiles.AddAsync(newFile);
            }

            await _datastore.SaveChangesAsync();

            return true;
        }

        public async Task<SubmissionReviewModel?> RetrieveSubmission(int submissionId)
        {
            var entity = await _datastore.Submissions.Include(s => s.Files.Where(f => !f.IsDeleted)).FirstOrDefaultAsync(s => s.Id == submissionId);
            if(entity == null)
                return null;
            return entity.ToReviewModel();
        }

        public async Task<List<SubmissionReviewModel>> RetrieveSubmissionListing()
        {
            var listing = _datastore.Submissions.Where(s => !s.IsDeleted).Select(s => s.ToReviewModel()).ToList();
            if(listing == null)
                return [];
            return listing;
        }

        public async Task<bool> AcceptSubmissions(EvidenceAcceptanceModel model)
        {
            var submission = await _datastore.Submissions.Include(s => s.Files.Where(f => !f.IsDeleted))
                                    .FirstOrDefaultAsync(s => s.Id == model.FileId);

            if(submission == null)
                return false;
            int processedCount = 0;
            foreach(var file in model.acceptedFiles)
            {
                var storedfile = await _datastore.StoredFiles.FirstOrDefaultAsync(f => f.Id == file);

                if(storedfile == null)
                    return false;

                await _fileStorage.AcceptAsync(storedfile, submission.TicketNumber);
                storedfile.IsDeleted = true;
                processedCount += 1;
            }

            if(processedCount == submission.Files.Count())
            {
                submission.IsDeleted = true;
            }
            await _datastore.SaveChangesAsync();
            return true;
        }

    }
}