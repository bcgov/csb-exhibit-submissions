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

        // Deterministic name/path for a submission's accepted package, so it can be
        // both written on Accept and retrieved later for download.
        private static string GetPackageName(Submission submission)
        {
            var shortDate = submission.UploadDate.HasValue
                ? submission.UploadDate.Value.ToString("yyyyMMdd")
                : "unknown";

            return $"{shortDate}_{submission.Id}.zip";
        }

        private string GetPackagePath(Submission submission)
            => Path.Combine(_options.AcceptedPath, GetPackageName(submission));

        public Task<Stream> GetAcceptedPackageAsync(Submission submission)
        {
            var zipPath = GetPackagePath(submission);

            if (!File.Exists(zipPath))
                throw new FileNotFoundException($"Accepted package for submission {submission.Id} not found", zipPath);

            Stream stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read);
            return Task.FromResult(stream);
        }

        public async Task AcceptSubmissionAsync(Submission submission)
        {
            var zipPath = GetPackagePath(submission);

            Directory.CreateDirectory(_options.AcceptedPath);

            // Collect retained (non-Removed) exhibits only
            var retainedFiles = submission.Files.Where(f => !f.IsDeleted).ToList();

            // Build unique entry names to avoid collisions inside the zip
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string UniqueEntryName(string original)
            {
                if (usedNames.Add(original)) return original;
                var ext = Path.GetExtension(original);
                var stem = Path.GetFileNameWithoutExtension(original);
                int n = 1;
                string candidate;
                do { candidate = $"{stem}_{n++}{ext}"; } while (!usedNames.Add(candidate));
                return candidate;
            }

            var fileHashes = new List<(string entryName, string hash, StoredFiles file)>();

            await using var zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 65536, useAsync: true);

            // ZipArchive must be disposed (writes Central Directory + EOCD) before zipStream is flushed.
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var file in retainedFiles)
                {
                    var sourcePath = Path.Combine(_options.LocalPath, file.StoredPath, file.StoredFileName);

                    if (!File.Exists(sourcePath))
                        throw new FileNotFoundException($"Stored file {file.OriginalFileName} not found", sourcePath);

                    var entryName = UniqueEntryName(file.OriginalFileName);
                    var hash = await Business.Services.CryptographyService.ComputeSHA256Async(sourcePath);
                    fileHashes.Add((entryName, hash, file));

                    var fileEntry = archive.CreateEntry(entryName);
                    using (var entryStream = fileEntry.Open())
                    await using (var fileStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 65536, useAsync: true))
                    {
                        await fileStream.CopyToAsync(entryStream);
                    }
                }

                // Combined metadata manifest (retained exhibits only)
                var metadataEntry = archive.CreateEntry("metadata.json");
                var metadata = new
                {
                    Submission = new
                    {
                        submission.Id,
                        submission.UploadDate,
                        submission.LocationId,
                        submission.LocationNameText,
                        submission.RoomCode,
                        submission.RoomText,
                        submission.OfficerNumber,
                        Tickets = submission.Tickets?.Select(t => new
                        {
                            t.AppearanceId,
                            t.AppearanceDateTime,
                            t.FileNumberText,
                            t.AccusedName,
                            t.AccusedDOB,
                        }),
                    },
                    HashAlgorithm = "SHA256",
                    Exhibits = fileHashes.Select(fh => new
                    {
                        fh.file.Id,
                        EntryName = fh.entryName,
                        fh.file.OriginalFileName,
                        fh.file.ContentType,
                        fh.file.FileSize,
                        fh.file.CreatedDateUTC,
                        fh.file.CreatedBy,
                        fh.file.Description,
                        fh.file.MarkedValue,
                        fh.file.MarkedAt,
                        fh.file.EnteredValue,
                        fh.file.EnteredAt,
                        SHA256 = fh.hash,
                    }),
                };

                using (var entryStream = metadataEntry.Open())
                {
                    await JsonSerializer.SerializeAsync(entryStream, metadata, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                }
            } // ZipArchive.Dispose() writes Central Directory + EOCD to zipStream here

            await zipStream.FlushAsync();
        }
    }
}
