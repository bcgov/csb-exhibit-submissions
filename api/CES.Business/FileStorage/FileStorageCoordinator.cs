using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Business.Services;
using CES.Entities;

namespace CES.Business.FileStorage
{
    // Composes the two independently-configured halves of the file store into the
    // single IFileStorage that SubmissionService and FileService consume.
    //
    // Why this exists: pending uploads and the accepted store have different
    // requirements — uploads want fast local disk, the accepted store wants a managed,
    // backed-up system of record — so FileStorage:PendingProvider and
    // FileStorage:AcceptedProvider are set independently. Keeping IFileStorage as the
    // consumer-facing seam means neither service, nor any test double, has to know
    // that the store has two halves at all.
    //
    // Everything that spans both halves lives here rather than in a provider, so the
    // verification rules hold for every pending × accepted pairing and are written
    // exactly once.
    public class FileStorageCoordinator : IFileStorage
    {
        private readonly IPendingFileStore _pending;
        private readonly IAcceptedFileStore _accepted;

        public FileStorageCoordinator(IPendingFileStore pending, IAcceptedFileStore accepted)
        {
            _pending = pending;
            _accepted = accepted;
        }

        public Task<StoredFiles> SaveAsync(FileUpload file, string storagePath)
            => _pending.SaveAsync(file, storagePath);

        public Task<Stream> GetAsync(StoredFiles storedFile)
            => _pending.GetAsync(storedFile);

        public Task DeleteAsync(StoredFiles storedFile)
            => _pending.DeleteAsync(storedFile);

        public Task WriteMetadataAsync(Submission submission, IReadOnlyList<SubmissionAuditLog> auditLogs)
            => _accepted.WriteMetadataAsync(submission, auditLogs);

        public Task<Stream> GetAcceptedExhibitAsync(StoredFiles file)
            => _accepted.GetAcceptedExhibitAsync(file);

        // Reads the pending bytes and hands them to the accepted store. The
        // already-accepted check runs first and does not touch the pending half: the
        // pending copy is deleted once an acceptance is committed, so requiring it
        // here would break every re-run after a successful cleanup.
        public async Task<AcceptedFileResult> PromoteToAcceptedAsync(Submission submission, StoredFiles file)
        {
            var existing = await _accepted.TryGetExistingAsync(submission, file);
            if (existing != null)
                return existing;

            if (!await _pending.ExistsAsync(file))
                throw new FileNotFoundException($"Pending file {file.OriginalFileName} not found");

            await using var pendingContent = await _pending.GetAsync(file);
            return await _accepted.PromoteToAcceptedAsync(submission, file, pendingContent);
        }

        // Removes the pending copy once the exhibit lives in the accepted store. Every
        // precondition is re-checked here rather than trusted from the promotion that
        // ran earlier in the request: the delete is irreversible, so it only happens
        // when the canonical copy is present and its bytes hash to both the value
        // recorded at acceptance and the bytes about to be deleted.
        public async Task<PendingCleanupResult> DeletePendingCopyAsync(StoredFiles file)
        {
            // Without a canonical path and hash there is nothing to verify against.
            if (!file.IsAccepted || string.IsNullOrEmpty(file.CanonicalPath) || string.IsNullOrEmpty(file.Sha256))
                return PendingCleanupResult.VerificationFailed;

            if (!await _pending.ExistsAsync(file))
                return PendingCleanupResult.AlreadyRemoved;

            Stream canonical;
            try
            {
                canonical = await _accepted.GetAcceptedExhibitAsync(file);
            }
            catch (FileNotFoundException)
            {
                // The accepted store is the only other copy — if it is gone, the
                // pending bytes are all that is left and must be kept.
                return PendingCleanupResult.VerificationFailed;
            }

            await using (canonical)
            await using (var pending = await _pending.GetAsync(file))
            {
                // Cheap gate first — a length mismatch rules out a complete copy
                // without reading either side end to end. Skipped rather than assumed
                // when a store cannot report a length up front.
                if (canonical.CanSeek && pending.CanSeek && canonical.Length != pending.Length)
                    return PendingCleanupResult.VerificationFailed;

                // Hash both sides now, immediately before the delete. Comparing the
                // canonical bytes to the DB hash proves the accepted copy is the one
                // that was accepted; comparing them to the pending bytes proves the
                // copy about to be deleted is fully represented there.
                var canonicalHash = await CryptographyService.ComputeSHA256Async(canonical);
                if (!string.Equals(canonicalHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
                    return PendingCleanupResult.VerificationFailed;

                var pendingHash = await CryptographyService.ComputeSHA256Async(pending);
                if (!string.Equals(pendingHash, canonicalHash, StringComparison.OrdinalIgnoreCase))
                    return PendingCleanupResult.VerificationFailed;
            }

            await _pending.DeleteAsync(file);
            return PendingCleanupResult.Deleted;
        }
    }
}
