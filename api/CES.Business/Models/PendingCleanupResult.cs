namespace CES.Business.Models
{
    // Outcome of removing the pending (uploads) copy of an exhibit after it has been
    // accepted into canonical storage. Removing the original is only ever safe once
    // the canonical copy has been re-verified, so every non-Deleted outcome means the
    // pending bytes were deliberately left in place.
    public enum PendingCleanupResult
    {
        // The canonical copy verified and the pending original was removed.
        Deleted,

        // Nothing to do — the pending original is already gone (e.g. a retry, or a
        // file accepted before the cleanup step existed and cleaned up since).
        AlreadyRemoved,

        // The file is not accepted, the canonical copy is missing, or its bytes do
        // not match the hash captured at acceptance. The pending copy is retained as
        // the surviving source of the exhibit and must be investigated.
        VerificationFailed,
    }
}
