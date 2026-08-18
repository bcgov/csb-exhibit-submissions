namespace CES.API.FileStorage.Smb
{
    // Defaults and hard bounds for the SMB accepted store (spec/smb-file-storage.md).
    // Project rule: no inline magic values — every number below is either the default
    // for a FileStorage:Smb setting or a bound the Stage 1 diagnostic relies on.
    public static class SmbConstants
    {
        // Accepted values for FileStorage:Smb:TransportType. Matched case-insensitively,
        // and the SMBLibrary enum names (DirectTCPTransport / NetBiosOverTCP) are also
        // accepted so a value copied out of the library reads correctly.
        public const string TransportDirectTcp = "DirectTcp";
        public const string TransportNetBios = "NetBios";

        // Accepted values for FileStorage:Smb:AuthenticationMethod. SMBLibrary is
        // NTLM-only — there is no Kerberos option (spec Q2).
        public const string AuthNtlmV2 = "NTLMv2";
        public const string AuthNtlmV1ExtendedSessionSecurity = "NTLMv1ExtendedSessionSecurity";
        public const string AuthNtlmV1 = "NTLMv1";

        // 64 KiB read/write chunk — matches AcceptedStorageConstants.CopyBufferSize so
        // the local and SMB stores move bytes in the same sized bites. Clamped at use
        // time to the server's negotiated MaxReadSize/MaxWriteSize.
        public const int DefaultBufferSize = 65536;

        // Ceiling on concurrent SMB sessions. A download holds its session for the whole
        // response, so this mainly bounds in-flight downloads against the file server's
        // session table.
        public const int DefaultMaxConcurrentSessions = 16;

        public const int DefaultConnectTimeoutMs = 10000;

        // Session establishment only. Reads are retried at a higher level (Stage 2);
        // writes are never retried (spec, Retry policy).
        public const int DefaultMaxRetryAttempts = 3;
        public const int DefaultInitialRetryDelayMs = 1000;

        // How long a caller waits for a slot under MaxConcurrentSessions before giving up.
        public const int SessionAcquireTimeoutMs = 30000;

        // The SMB2 path separator. Paths handed to CreateFile are share-relative and
        // carry neither a leading nor a trailing separator; "" is the share root.
        public const char Separator = '\\';

        // --- Stage 1 diagnostic bounds -------------------------------------------------

        // Wildcard handed to QueryDirectory. Listing happens only in the diagnostic —
        // production paths are exact and self-authored, so they never enumerate.
        public const string DirectorySearchPattern = "*";

        // The diagnostic reports the first N names of a folder; it is a shape probe, not
        // a directory browser.
        public const int MaxDiagnosticDirectoryEntries = 50;

        // Upper bound on the bytes the probe read pulls back, so pointing ProbeFile at a
        // large file cannot turn a health check into a full download.
        public const int MaxProbeReadBytes = 1048576; // 1 MiB

        // QueryDirectory returns these two on every folder; they are not entries.
        public const string CurrentDirectoryEntry = ".";
        public const string ParentDirectoryEntry = "..";

        // Step names. They double as the keys of the diagnostic response and as
        // SmbException.Step, so a failure reported in a log and a failure reported by
        // /api/dev/smb/health name the same thing.
        public const string StepConnect = "connect";
        public const string StepLogin = "login";
        public const string StepListShares = "listShares";
        public const string StepTreeConnect = "treeConnect";
        public const string StepListBasePath = "listBasePath";
        public const string StepProbeRead = "probeRead";
    }
}
