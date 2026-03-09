using CES.Business.Interfaces;
using CES.Entities;
using CES.Entities.Interfaces;

namespace CES.Business.Services
{
    public class FileService : IFileService
    {
        private readonly ICESDataStore _dataStore;

        public FileService (ICESDataStore dataStore)
        {
            _dataStore = dataStore;
        }
        public async Task<StoredFiles?> RetrieveFileMetaData(Guid fileId)
        {
            return await _dataStore.StoredFiles.FindAsync(fileId);
        }
    }
}