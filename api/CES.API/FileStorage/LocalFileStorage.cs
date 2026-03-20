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
                StorageProvider = "Local"
            };
        }

        public Task<Stream> GetAsync(StoredFiles storedFile)
        {
            var path = Path.Combine(_options.LocalPath, storedFile.StoredPath, storedFile.StoredFileName);
            Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            return Task.FromResult(stream);
        }

        public Task DeleteAsync(StoredFiles storedFile)
        {
            var path = Path.Combine(_options.LocalPath, storedFile.StoredPath, storedFile.StoredFileName);
            
            if (!File.Exists(path))
                throw new FileNotFoundException($"Stored file {storedFile.OriginalFileName} not found", path);

            File.Delete(path);
            return Task.CompletedTask;
        }

        public async Task AcceptAsync(StoredFiles file)
        {
            var sourcePath = Path.Combine(_options.LocalPath, file.StoredPath, file.StoredFileName);

            if (!File.Exists(sourcePath))
                throw new FileNotFoundException($"Stored file {file.OriginalFileName} not found", sourcePath);

            var acceptedPath = Path.Combine(_options.AcceptedPath, file.StoredPath);
            Directory.CreateDirectory(acceptedPath);

            var zipName = $"{file.CreatedDateUTC:yyyyMMdd}_{file.Id}.zip";
            var zipPath = Path.Combine(acceptedPath, zipName);

            var hash = await CES.Business.Services.CryptographyService.ComputeSHA256Async(sourcePath);

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

            var metadata = new {
                                    Path = file.StoredPath,
                                    File = file,
                                    HashAlgorithm = "SHA256",
                                    FileHash = hash
                                };
            using (var entryStream = metadataEntry.Open())
            {
                await JsonSerializer.SerializeAsync(entryStream, metadata, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
            }

            // sha256.txt
            var hashEntry = archive.CreateEntry("sha256.txt");

            using (var entryStream = new StreamWriter(hashEntry.Open()))
            {
                await entryStream.WriteLineAsync($"File: {file.OriginalFileName}");
                await entryStream.WriteLineAsync($"SHA256: {hash}");
            }
        
        }
    }
}