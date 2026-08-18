using Microsoft.Extensions.Logging;
using SMBLibrary;
using SMBLibrary.Client;

namespace CES.API.FileStorage.Smb
{
    // One connected, logged-in SMB session, optionally tree-connected to a share.
    // Disposing it tears down the tree connect, the session and the TCP connection, and
    // releases the concurrency slot the factory took out on its behalf.
    //
    // SMB2Client is not thread-safe, so a session belongs to exactly one operation.
    // The exception is a download, where SmbReadStream owns its session for the life of
    // the stream (Stage 2) — still one operation, just a long one.
    public sealed class SmbSession : IDisposable
    {
        private readonly SMB2Client _client;
        private readonly IDisposable? _concurrencySlot;
        private readonly ILogger? _logger;

        private ISMBFileStore? _fileStore;
        private bool _disposed;

        internal SmbSession(
            SMB2Client client,
            long connectElapsedMs,
            long loginElapsedMs,
            IDisposable? concurrencySlot,
            ILogger? logger)
        {
            _client = client;
            _concurrencySlot = concurrencySlot;
            _logger = logger;
            ConnectElapsedMs = connectElapsedMs;
            LoginElapsedMs = loginElapsedMs;
        }

        public long ConnectElapsedMs { get; }

        public long LoginElapsedMs { get; }

        public bool IsTreeConnected => _fileStore != null;

        public string? ShareName { get; private set; }

        // What the server agreed to during negotiation. These are the only negotiated
        // facts SMBLibrary 1.5.3 exposes publicly — the dialect and whether the session
        // is encrypted are private fields (spec, Verified against SMBLibrary 1.5.3).
        public SmbNegotiatedLimits Limits =>
            new(_client.MaxReadSize, _client.MaxWriteSize, _client.MaxTransactSize);

        public INTFileStore FileStore =>
            _fileStore ?? throw new InvalidOperationException(
                "SMB session is not tree-connected. Call TreeConnect first, or use ISmbSessionFactory.OpenShareAsync.");

        // The server's shares, via IPC$/SRVSVC. Needs the login but not a tree connect,
        // which is what makes it usable to discover a share name we do not have yet.
        // A hardened server may answer STATUS_ACCESS_DENIED here while normal share
        // access works fine, so the status is returned rather than thrown.
        public IReadOnlyList<string> ListShares(out NTStatus status)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var shares = _client.ListShares(out status);
            return shares ?? [];
        }

        // Attempts the tree connect and hands back the raw status. Used by the
        // diagnostic, which reports every step rather than failing on the first.
        public NTStatus TryTreeConnect(string shareName)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (string.IsNullOrWhiteSpace(shareName))
                throw new ArgumentException("Share name is required.", nameof(shareName));

            var fileStore = _client.TreeConnect(shareName, out var status);

            if (status == NTStatus.STATUS_SUCCESS && fileStore != null)
            {
                _fileStore = fileStore;
                ShareName = shareName;
            }

            return status;
        }

        // Production form: tree-connect or fail with the status attached.
        public void TreeConnect(string shareName)
        {
            var status = TryTreeConnect(shareName);

            if (status != NTStatus.STATUS_SUCCESS)
                throw new SmbException(
                    SmbConstants.StepTreeConnect, status,
                    $"SMB tree connect to share '{shareName}' failed with {status}.");
        }

        // Each teardown step is attempted independently: a failure part-way through must
        // not leave the socket open or the concurrency slot held.
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            TryTeardown(() =>
            {
                if (_fileStore != null)
                {
                    _fileStore.Disconnect();
                    _fileStore = null;
                }
            });

            TryTeardown(() => _client.Logoff());
            TryTeardown(() => _client.Disconnect());
            TryTeardown(() => _concurrencySlot?.Dispose());
        }

        private void TryTeardown(Action step)
        {
            try
            {
                step();
            }
            catch (Exception ex)
            {
                // Best-effort: the session is going away regardless, and throwing here
                // would mask whatever the caller was actually doing.
                _logger?.LogWarning(ex, "Error tearing down SMB session.");
            }
        }
    }

    public sealed record SmbNegotiatedLimits(uint MaxReadSize, uint MaxWriteSize, uint MaxTransactSize);
}
