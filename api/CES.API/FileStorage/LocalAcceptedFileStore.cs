using System.Security.Cryptography;
using CES.Business.Constants;
using CES.Business.FileStorage;
using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Business.Services;
using CES.Entities;
using Microsoft.Extensions.Options;

namespace CES.API.FileStorage
{
    // The accepted (canonical) exhibit store on pod-local disk, under
    // FileStorage:AcceptedPath. Selected by FileStorage:AcceptedProvider = "Local".
    //
    // Fine for local development; for a real system of record the pod's disk is
    // ephemeral and unmanaged, which is what the Smb provider addresses
    // (spec/smb-file-storage.md).
    public class LocalAcceptedFileStore : IAcceptedFileStore
    {
        private readonly StorageOptions _options;

        public LocalAcceptedFileStore(IOptions<StorageOptions> options)
        {
            _options = options.Value;
        }

        // Idempotency probe: if the canonical bytes are already present (already
        // accepted), re-hash them so the caller can persist/verify without re-copying.
        public async Task<AcceptedFileResult?> TryGetExistingAsync(Submission submission, StoredFiles file)
        {
            var extension = Path.GetExtension(file.OriginalFileName);
            var relativePath = BuildRelativePath(submission, file, extension);
            var destinationFullPath = AcceptedPathBuilder.ResolveAndVerifyWithinRoot(_options.AcceptedPath, relativePath);

            if (!File.Exists(destinationFullPath))
                return null;

            return new AcceptedFileResult
            {
                CanonicalPath = relativePath,
                AcceptedFileName = AcceptedPathBuilder.BuildAcceptedFileName(file.Id, extension),
                Sha256 = await CryptographyService.ComputeSHA256Async(destinationFullPath),
            };
        }

        // Copies a pending file's bytes into the submission's accepted folder once,
        // computes SHA256, and returns the canonical location. Single-instance is
        // structural: one submission → one folder, so an exhibit shared across N
        // tickets is physically one file by construction.
        public async Task<AcceptedFileResult> PromoteToAcceptedAsync(Submission submission, StoredFiles file, Stream pendingContent)
        {
            var extension = Path.GetExtension(file.OriginalFileName);
            var relativePath = BuildRelativePath(submission, file, extension);
            var acceptedFileName = AcceptedPathBuilder.BuildAcceptedFileName(file.Id, extension);

            var destinationFullPath = AcceptedPathBuilder.ResolveAndVerifyWithinRoot(_options.AcceptedPath, relativePath);

            var destinationFolder = Path.GetDirectoryName(destinationFullPath)!;
            Directory.CreateDirectory(destinationFolder);

            // Atomic byte placement: copy to {exhibitId}{ext}.tmp, verify, then rename.
            // Verification is what licenses deleting the pending original later, so an
            // unverified copy must never become canonical: the hash is accumulated over
            // the bytes read from the source during the copy and then re-read back off
            // the written temp file. A mismatch (short write, full disk, bad sector)
            // deletes the temp and throws, leaving the pending original untouched.
            var tempPath = destinationFullPath + AcceptedStorageConstants.TempSuffix;
            string hash;

            try
            {
                var (sourceHash, bytesCopied) = await CopyAndHashAsync(pendingContent, tempPath);

                var writtenLength = new FileInfo(tempPath).Length;
                if (writtenLength != bytesCopied)
                    throw new IOException($"Accepted copy of {file.OriginalFileName} is {writtenLength} bytes, expected {bytesCopied}.");

                var writtenHash = await CryptographyService.ComputeSHA256Async(tempPath);
                if (!string.Equals(writtenHash, sourceHash, StringComparison.OrdinalIgnoreCase))
                    throw new IOException($"Accepted copy of {file.OriginalFileName} failed {AcceptedStorageConstants.HashAlgorithm} verification.");

                hash = writtenHash;
            }
            catch
            {
                TryDeleteTempFile(tempPath);
                throw;
            }

            File.Move(tempPath, destinationFullPath, overwrite: true);

            return new AcceptedFileResult
            {
                CanonicalPath = relativePath,
                AcceptedFileName = acceptedFileName,
                Sha256 = hash,
            };
        }

        public async Task WriteMetadataAsync(Submission submission, IReadOnlyList<SubmissionAuditLog> auditLogs)
        {
            var folderRelative = AcceptedPathBuilder.BuildSubmissionFolderRelativePath(
                submission.LocationId, submission.RoomCode, submission.ShortDate, submission.Id);
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

        private static string BuildRelativePath(Submission submission, StoredFiles file, string? extension)
            => AcceptedPathBuilder.BuildCanonicalRelativePath(
                submission.LocationId, submission.RoomCode, submission.ShortDate,
                submission.Id, file.Id, extension);

        // Streams source → destination while hashing the bytes as they are read, so the
        // copy costs one pass over the source rather than a separate hashing pass.
        // Returns the source hash and the number of bytes written.
        private static async Task<(string Sha256, long Length)> CopyAndHashAsync(Stream source, string destinationPath)
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[AcceptedStorageConstants.CopyBufferSize];
            long total = 0;

            await using (var dest = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, AcceptedStorageConstants.CopyBufferSize, useAsync: true))
            {
                int read;
                while ((read = await source.ReadAsync(buffer)) > 0)
                {
                    await dest.WriteAsync(buffer.AsMemory(0, read));
                    hash.AppendData(buffer, 0, read);
                    total += read;
                }

                await dest.FlushAsync();
                // Force the bytes down to the device, not just the OS cache: the pending
                // original is deleted once this copy verifies, so the accepted copy has
                // to survive a crash on its own.
                dest.Flush(flushToDisk: true);
            }

            return (Convert.ToHexString(hash.GetHashAndReset()), total);
        }

        // Best-effort removal of a failed partial copy. A leftover .tmp is harmless
        // (the next promotion overwrites it), so a failure here must not mask the
        // original error being propagated.
        private static void TryDeleteTempFile(string tempPath)
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
