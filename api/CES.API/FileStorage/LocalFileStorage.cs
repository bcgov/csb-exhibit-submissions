using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Entities;
using Microsoft.Extensions.Options;
using System.IO.Compression;
using System.Text.Json;

namespace CES.API.FileStorage
{
    public class LocalFileStorage : IFileStorage
    {
        private readonly StorageOptions _options;

        public LocalFileStorage(IOptions<StorageOptions> options)
        {
            _options = options.Value;
        }

        public async Task<StoredFiles> SaveAsync(FileUpload file, string ticketNumber)
        {
            if (file.Length > _options.MaxFileSize)
                throw new Exception("File too large");
            var path = Path.Combine(_options.LocalPath, ticketNumber);

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

        public async Task AcceptAsync(StoredFiles file, string ticketNumber)
        {
            var sourcePath = Path.Combine(_options.LocalPath, ticketNumber, file.StoredFileName);

            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Stored file not found", sourcePath);

            var acceptedPath = Path.Combine(_options.AcceptedPath, ticketNumber);
            Directory.CreateDirectory(acceptedPath);

            var zipName = $"{file.CreatedDateUTC:yyyyMMdd}_{file.Id}.zip";
            var zipPath = Path.Combine(acceptedPath, zipName);

            using var zipStream = new FileStream(zipPath, FileMode.Create);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

            // Add original file
            var fileEntry = archive.CreateEntry(file.OriginalFileName);

            using (var entryStream = fileEntry.Open())
            using (var fileStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
            {
                await fileStream.CopyToAsync(entryStream);
            }

            // Create metadata.json
            var metadataEntry = archive.CreateEntry("metadata.json");

            using (var entryStream = metadataEntry.Open())
                await JsonSerializer.SerializeAsync(entryStream, file, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
        
        }
    }
}