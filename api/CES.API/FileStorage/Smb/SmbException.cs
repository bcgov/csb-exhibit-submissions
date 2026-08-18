using SMBLibrary;

namespace CES.API.FileStorage.Smb
{
    // A failed SMB operation, carrying the step that failed and the raw NTStatus the
    // server returned. The status is kept unflattened on purpose: STATUS_LOGON_FAILURE,
    // STATUS_ACCESS_DENIED, STATUS_BAD_NETWORK_NAME and STATUS_OBJECT_PATH_NOT_FOUND are
    // four entirely different conversations, and collapsing them into "connection
    // failed" is how a half-day gets lost (spec, Stage 1).
    //
    // Derives from IOException so it lands on the same 500 as any other storage fault in
    // ApiExceptionMiddleware; the diagnostic endpoint reports Step/Status directly.
    public class SmbException : IOException
    {
        public SmbException(string step, NTStatus? status, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            Step = step;
            Status = status;
        }

        // Name of the SMB step that failed: connect, login, treeConnect, …
        public string Step { get; }

        // The server's raw NTStatus, or null when the failure happened below the SMB
        // layer (DNS, TCP, timeout) and there was no response to read a status from.
        public NTStatus? Status { get; }

        // How long the failing step took, when the caller measured it.
        public long ElapsedMs { get; init; }

        // Retry only when the server never answered. A returned NTStatus is a real
        // answer — a wrong domain or a wrong share name is not transient, and retrying
        // it just multiplies the wait before the operator sees the real reason.
        public bool IsRetryable => Status is null;

        public string StatusName => Status?.ToString() ?? string.Empty;
    }
}
