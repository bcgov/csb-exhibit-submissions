
using CES.Business.Models;
using CES.Entities;

namespace CES.Business.Interfaces
{
    public interface IFileStorage
    {
        Task<StoredFiles> SaveAsync(FileUpload file, string storagePath);
        Task<Stream> GetAsync(StoredFiles storedFile);
        Task DeleteAsync(StoredFiles storedFile);

        // Copies a pending file's bytes into the submission's accepted folder once,
        // computes SHA256, and returns the canonical location. Idempotent: if the
        // file is already accepted (bytes already present) it does not re-copy.
        Task<AcceptedFileResult> PromoteToAcceptedAsync(Submission submission, StoredFiles file);

        // (Re)writes the single metadata.json in the submission's accepted folder,
        // derived from DB truth. Revisions are sourced from the submission audit log.
        Task WriteMetadataAsync(Submission submission, IReadOnlyList<SubmissionAuditLog> auditLogs);

        // Opens an accepted exhibit by its canonical path (resolved + verified within
        // root). Throws if the canonical file is missing (fail safe).
        Task<Stream> GetAcceptedExhibitAsync(StoredFiles file);

        // Removes the pending (uploads) copy of an accepted file — but only after
        // re-verifying that the canonical copy exists and that its bytes still hash to
        // the value captured at acceptance. Reports the outcome instead of throwing on
        // a failed verification, so the pending bytes are never dropped on a doubt.
        Task<PendingCleanupResult> DeletePendingCopyAsync(StoredFiles file);
    }
}
