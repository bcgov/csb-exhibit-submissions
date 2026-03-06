using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Entities;
using Microsoft.Extensions.Options;


namespace CES.API.FileStorage
{
    public class LocalFileStorage : IFileStorage
    {
        private readonly StorageOptions _options;

        public LocalFileStorage(IOptions<StorageOptions> options)
        {
            _options = options.Value;
        }

        public async Task<StoredFiles> SaveAsync(FileUpload file)
        {
            if (file.Length > _options.MaxFileSize)
                throw new Exception("File too large");

            Directory.CreateDirectory(_options.LocalPath);

            var fileGuid = Guid.NewGuid();

            var storedName = $"{fileGuid}{Path.GetExtension(file.FileName)}";

            var path = Path.Combine(_options.LocalPath, storedName);

            using var fs = new FileStream(path, FileMode.Create);
            await file.Content.CopyToAsync(fs);

            return new StoredFiles
            {
                Id = fileGuid,
                OriginalFileName = file.FileName,
                StoredFileName = storedName,
                ContentType = file.ContentType,
                FileSize = file.Length,
                StorageProvider = "Local"
            };
        }

        public Task<Stream> GetAsync(string storedFileName)
        {
            var path = Path.Combine(_options.LocalPath, storedFileName);
            Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            return Task.FromResult(stream);
        }

        public Task DeleteAsync(string storedFileName)
        {
            var path = Path.Combine(_options.LocalPath, storedFileName);
            File.Delete(path);
            return Task.CompletedTask;
        }
    }
}