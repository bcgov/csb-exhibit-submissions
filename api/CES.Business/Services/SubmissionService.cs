using CES.Business.Extensions.Entities;
using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Entities.Interfaces;

namespace CES.Business.Services
{
    public class SubmissionService : ISubmissionService
    {
        private ICESDataStore _datastore;
        public SubmissionService (ICESDataStore dataStore)
        {
            _datastore = dataStore;
            
        }
        public async Task<bool> SubmitEvidence(EvidenceSubmissionModel model)
        {

            var entity = model.ToEntity();

            // foreach (var file in model.fileUploads)
            // {
            //     var path = Path.Combine("uploads", file.FileName);

            //     using var fileStream = new FileStream(path, FileMode.Create);
            //     await file.Content.CopyToAsync(fileStream);
            // }

            _datastore.Submissions.Add(entity);
            await _datastore.SaveChangesAsync();

            return true;
        }
    }
}