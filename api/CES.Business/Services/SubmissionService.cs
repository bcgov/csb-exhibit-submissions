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

            foreach (var file in model.fileUploads)
            {
                var newFile = await _fileStorage.SaveAsync(file);
                entity.Files.Add(newFile);
                await _datastore.StoredFiles.AddAsync(newFile);
            }

            await _datastore.Submissions.AddAsync(entity);
            await _datastore.SaveChangesAsync();

            return true;
        }

        public async Task<SubmissionReviewModel?> RetrieveSubmission(int submissionId)
        {
            var entity = await _datastore.Submissions.Include(s => s.Files).FirstOrDefaultAsync(s => s.Id == submissionId);
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

    }
}