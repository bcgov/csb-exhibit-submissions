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

            await using var zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 65536, useAsync: true);

            // ZipArchive must be disposed (writes Central Directory + EOCD) before zipStream is flushed.
            // leaveOpen: true lets us control zipStream lifetime separately via await using above.
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var fileEntry = archive.CreateEntry(file.OriginalFileName);
                using (var entryStream = fileEntry.Open())
                await using (var fileStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 65536, useAsync: true))
                {
                    await fileStream.CopyToAsync(entryStream);
                }

                var metadataEntry = archive.CreateEntry("metadata.json");
                var metadata = new
                {
                    Path = file.StoredPath,
                    HashAlgorithm = "SHA256",
                    FileHash = hash,
                    File = new
                    {
                        file.Id,
                        file.OriginalFileName,
                        file.ContentType,
                        file.FileSize,
                        file.CreatedDateUTC,
                        file.CreatedBy,
                        file.Description,
                        file.MarkedValue,
                        file.MarkedAt,
                        file.EnteredValue,
                        file.EnteredAt,
                    },
                    Submission = file.Submission == null ? null : new
                    {
                        file.Submission.UploadDate,
                        file.Submission.LocationId,
                        file.Submission.LocationNameText,
                        file.Submission.RoomCode,
                        file.Submission.RoomText,
                        file.Submission.OfficerNumber,
                        Tickets = file.Submission.Tickets?.Select(t => new
                        {
                            t.AppearanceId,
                            t.AppearanceDateTime,
                            t.FileNumberText,
                            t.AccusedName,
                            t.AccusedDOB,
                        }),
                    },
                };
                using (var entryStream = metadataEntry.Open())
                {
                    await JsonSerializer.SerializeAsync(entryStream, metadata, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                }

                var hashEntry = archive.CreateEntry("sha256.txt");
                await using (var writer = new StreamWriter(hashEntry.Open()))
                {
                    await writer.WriteLineAsync($"File: {file.OriginalFileName}");
                    await writer.WriteLineAsync($"SHA256: {hash}");
                    await writer.WriteLineAsync($"Description: {file.Description ?? "—"}");
                    await writer.WriteLineAsync($"Marked: {file.MarkedValue ?? "—"}");
                    await writer.WriteLineAsync($"Marked At: {(file.MarkedAt.HasValue ? file.MarkedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") + " UTC" : "—")}");
                    await writer.WriteLineAsync($"Entered: {file.EnteredValue ?? "—"}");
                    await writer.WriteLineAsync($"Entered At: {(file.EnteredAt.HasValue ? file.EnteredAt.Value.ToString("yyyy-MM-dd HH:mm:ss") + " UTC" : "—")}");
                }
            } // ZipArchive.Dispose() writes Central Directory + EOCD to zipStream here

            await zipStream.FlushAsync(); // ensure all bytes reach disk before DisposeAsync closes the file
        }
    }
}