using CES.Business.Constants;
using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Entities;
using Microsoft.Extensions.Options;

namespace CES.API.FileStorage
{
    // Pending uploads on pod-local disk, under FileStorage:LocalPath.
    // Selected by FileStorage:PendingProvider = "Local".
    public class LocalPendingFileStore : IPendingFileStore
    {
        private readonly StorageOptions _options;

        public LocalPendingFileStore(IOptions<StorageOptions> options)
        {
            _options = options.Value;
        }

        public async Task<StoredFiles> SaveAsync(FileUpload file, string storagePath)
        {
            if (file.Length > _options.MaxFileSize)
                throw new Exception("File too large");
            var path = Path.Combine(_options.LocalPath, storagePath);

            Directory.CreateDirectory(path);

            var fileGuid = Guid.NewGuid();

            var storedName = $"{fileGuid}{Path.GetExtension(file.FileName)}";

            using var fs = new FileStream(Path.Combine(path, storedName), FileMode.Create);
            await file.Content.CopyToAsync(fs);

            return new StoredFiles
            {
                Id = fileGuid,
                OriginalFileName = file.FileName,
                StoredFileName = storedName,
                StoredPath = $"{storagePath}",
                ContentType = file.ContentType,
                FileSize = file.Length,
                StorageProvider = FileStorageProviders.Local
            };
        }

        public Task<Stream> GetAsync(StoredFiles storedFile)
        {
            Stream stream = new FileStream(FullPath(storedFile), FileMode.Open, FileAccess.Read);
            return Task.FromResult(stream);
        }

        public Task DeleteAsync(StoredFiles storedFile)
        {
            var path = FullPath(storedFile);

            if (!File.Exists(path))
                throw new FileNotFoundException($"Stored file {storedFile.OriginalFileName} not found", path);

            File.Delete(path);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(StoredFiles storedFile)
            => Task.FromResult(File.Exists(FullPath(storedFile)));

        private string FullPath(StoredFiles storedFile)
            => Path.Combine(_options.LocalPath, storedFile.StoredPath, storedFile.StoredFileName);
    }
}
