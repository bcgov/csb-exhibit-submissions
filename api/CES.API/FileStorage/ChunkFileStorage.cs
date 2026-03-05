using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Entities;
using Microsoft.Extensions.Options;

namespace CES.API.FileStorage
{
    public class ChunkFileStorage : IFileStorage
    {
        private readonly StorageOptions _options;

        public ChunkFileStorage(IOptions<StorageOptions> options)
        {
            _options = options.Value;
        }

        public Task DeleteAsync(string storedFileName)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> GetAsync(string storedFileName)
        {
            throw new NotImplementedException();
        }

        public Task<StoredFiles> SaveAsync(FileUpload file)
        {
            throw new NotImplementedException();
        }

        public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string contentType)
        {
            return "";
        }
    }
}