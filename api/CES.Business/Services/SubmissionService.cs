using CES.Business.Extensions.Entities;
using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Entities.Interfaces;

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
                await _datastore.StoredFiles.AddAsync(newFile);
            }

            await _datastore.Submissions.AddAsync(entity);
            await _datastore.SaveChangesAsync();

            return true;
        }
    }
}