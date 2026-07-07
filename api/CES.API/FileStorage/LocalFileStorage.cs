using CES.Business.Constants;
using CES.Business.FileStorage;
using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Business.Services;
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

        // Copies a pending file's bytes into the submission's accepted folder once,
        // computes SHA256, and returns the canonical location. Single-instance is
        // structural: one submission → one folder, so an exhibit shared across N
        // tickets is physically one file by construction.
        public async Task<AcceptedFileResult> PromoteToAcceptedAsync(Submission submission, StoredFiles file)
        {
            var extension = Path.GetExtension(file.OriginalFileName);
            var relativePath = AcceptedPathBuilder.BuildCanonicalRelativePath(
                submission.LocationId, submission.RoomCode, submission.ShortDate,
                submission.Id, file.Id, extension);
            var acceptedFileName = AcceptedPathBuilder.BuildAcceptedFileName(file.Id, extension);

            var destinationFullPath = AcceptedPathBuilder.ResolveAndVerifyWithinRoot(_options.AcceptedPath, relativePath);

            // Idempotent: if the canonical bytes are already present (already accepted),
            // do not re-copy. Re-hash the canonical file so the caller can persist/verify.
            if (File.Exists(destinationFullPath))
            {
                var existingHash = await CryptographyService.ComputeSHA256Async(destinationFullPath);
                return new AcceptedFileResult
                {
                    CanonicalPath = relativePath,
                    AcceptedFileName = acceptedFileName,
                    Sha256 = existingHash,
                };
            }

            var sourcePath = Path.Combine(_options.LocalPath, file.StoredPath, file.StoredFileName);
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException($"Pending file {file.OriginalFileName} not found", sourcePath);

            var destinationFolder = Path.GetDirectoryName(destinationFullPath)!;
            Directory.CreateDirectory(destinationFolder);

            // Atomic byte placement: copy to {exhibitId}{ext}.tmp then rename.
            var tempPath = destinationFullPath + AcceptedStorageConstants.TempSuffix;
            await using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 65536, useAsync: true))
            await using (var dest = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 65536, useAsync: true))
            {
                await source.CopyToAsync(dest);
                await dest.FlushAsync();
            }

            File.Move(tempPath, destinationFullPath, overwrite: true);

            var hash = await CryptographyService.ComputeSHA256Async(destinationFullPath);

            return new AcceptedFileResult
            {
                CanonicalPath = relativePath,
                AcceptedFileName = acceptedFileName,
                Sha256 = hash,
            };
        }

        public async Task WriteMetadataAsync(Submission submission, IReadOnlyList<SubmissionAuditLog> auditLogs)
        {
            // Resolve the submission folder (loc/room/date/submissionId) within root.
            var loc = AcceptedPathBuilder.SanitizeSegment(submission.LocationId);
            var room = AcceptedPathBuilder.SanitizeSegment(submission.RoomCode);
            var date = AcceptedPathBuilder.SanitizeSegment(submission.ShortDate);
            var folderRelative = string.Join('/', loc, room, date, submission.Id.ToString());
            var folderFullPath = AcceptedPathBuilder.ResolveAndVerifyWithinRoot(_options.AcceptedPath, folderRelative);

            var metadata = AcceptedMetadataWriter.BuildMetadata(submission, auditLogs);
            await AcceptedMetadataWriter.WriteAsync(folderFullPath, metadata);
        }

        public Task<Stream> GetAcceptedExhibitAsync(StoredFiles file)
        {
            if (!file.IsAccepted || string.IsNullOrEmpty(file.CanonicalPath))
                throw new FileNotFoundException($"Exhibit {file.OriginalFileName} is not accepted.");

            var fullPath = AcceptedPathBuilder.ResolveAndVerifyWithinRoot(_options.AcceptedPath, file.CanonicalPath);

            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Accepted exhibit {file.OriginalFileName} not found", fullPath);

            Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            return Task.FromResult(stream);
        }
    }
}
