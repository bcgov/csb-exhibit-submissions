using CES.Business.Models;
using CES.Entities;

namespace CES.Business.Interfaces
{
    // The accepted (post-classification) half of the file store: the canonical,
    // immutable system of record for an exhibit, plus its metadata.json sidecar.
    //
    // Deliberately knows nothing about where the pending copy lives — bytes arrive as
    // a Stream the coordinator opened from whichever pending store is configured.
    // That is what allows the accepted half to move to a remote share while uploads
    // stay on pod-local disk (see spec/smb-file-storage.md).
    public interface IAcceptedFileStore
    {
        // Returns the canonical location and hash if this exhibit's bytes are already
        // present, otherwise null. Keeps promotion idempotent without requiring the
        // pending copy to still exist — it is deleted after a successful acceptance,
        // so a re-run must not depend on it.
        Task<AcceptedFileResult?> TryGetExistingAsync(Submission submission, StoredFiles file);

        // Streams pendingContent into the submission's accepted folder, verifies the
        // written bytes, and returns the canonical location + SHA256. Call only after
        // TryGetExistingAsync has returned null. Does not dispose pendingContent.
        Task<AcceptedFileResult> PromoteToAcceptedAsync(Submission submission, StoredFiles file, Stream pendingContent);

        // (Re)writes the single metadata.json in the submission's accepted folder,
        // derived from DB truth.
        Task WriteMetadataAsync(Submission submission, IReadOnlyList<SubmissionAuditLog> auditLogs);

        // Opens an accepted exhibit by its canonical path. Throws
        // FileNotFoundException if the canonical file is missing (fail safe).
        Task<Stream> GetAcceptedExhibitAsync(StoredFiles file);
    }
}
