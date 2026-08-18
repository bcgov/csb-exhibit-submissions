using System.Diagnostics;
using Microsoft.Extensions.Options;
using SMBLibrary;
using SMBLibrary.Client;

namespace CES.API.FileStorage.Smb
{
    // Establishes SMB sessions: connect → login → (optionally) tree-connect, with a
    // bounded retry and a concurrency cap.
    //
    // Registered as a singleton because the concurrency semaphore is process-wide; the
    // sessions it hands out are not shared and must be disposed by the caller.
    public sealed class SmbSessionFactory : ISmbSessionFactory, IDisposable
    {
        private readonly SmbOptions _options;
        private readonly ILogger<SmbSessionFactory> _logger;
        private readonly SemaphoreSlim _concurrency;

        public SmbSessionFactory(IOptions<SmbOptions> options, ILogger<SmbSessionFactory> logger)
        {
            _options = options.Value;
            _logger = logger;

            var maxSessions = _options.MaxConcurrentSessions > 0
                ? _options.MaxConcurrentSessions
                : SmbConstants.DefaultMaxConcurrentSessions;

            _concurrency = new SemaphoreSlim(maxSessions, maxSessions);
        }

        public Task<SmbSession> ConnectAsync(CancellationToken cancellationToken = default)
            => EstablishWithRetryAsync(shareName: null, cancellationToken);

        public Task<SmbSession> OpenShareAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.ShareName))
                throw new InvalidOperationException(
                    "FileStorage:Smb:ShareName is not configured. Run GET /api/dev/smb/health to list the server's shares.");

            return EstablishWithRetryAsync(_options.ShareName, cancellationToken);
        }

        // Retry covers session establishment only, and only when the server never
        // answered — see SmbException.IsRetryable. Reads are retried by their caller
        // (Stage 2); writes are never retried, because re-running a partially completed
        // multi-chunk write can truncate or double-write the file (spec, Retry policy).
        private async Task<SmbSession> EstablishWithRetryAsync(string? shareName, CancellationToken cancellationToken)
        {
            var maxAttempts = Math.Max(1, _options.MaxRetryAttempts);
            var delayMs = Math.Max(0, _options.InitialRetryDelayMs);

            for (var attempt = 1; ; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    return await EstablishAsync(shareName, cancellationToken);
                }
                catch (SmbException ex) when (ex.IsRetryable && attempt < maxAttempts)
                {
                    _logger.LogWarning(ex,
                        "SMB session attempt {Attempt}/{MaxAttempts} failed at step {Step}; retrying in {DelayMs}ms.",
                        attempt, maxAttempts, ex.Step, delayMs);

                    if (delayMs > 0)
                        await Task.Delay(delayMs, cancellationToken);

                    // Exponential backoff from InitialRetryDelayMs.
                    delayMs *= 2;
                }
            }
        }

        private async Task<SmbSession> EstablishAsync(string? shareName, CancellationToken cancellationToken)
        {
            var slot = await AcquireSlotAsync(cancellationToken);

            SMB2Client? client = null;
            SmbSession? session = null;

            try
            {
                client = new SMB2Client();

                var connectElapsedMs = Connect(client);
                var loginElapsedMs = Login(client);

                session = new SmbSession(client, connectElapsedMs, loginElapsedMs, slot, _logger);

                if (!string.IsNullOrWhiteSpace(shareName))
                    session.TreeConnect(shareName);

                return session;
            }
            catch
            {
                // Ownership of the client and the slot passes to the session once it
                // exists; before that, unwind them here.
                if (session != null)
                {
                    session.Dispose();
                }
                else
                {
                    TryDisconnect(client);
                    slot.Dispose();
                }

                throw;
            }
        }

        // Returns how long the step took, so a successful session and a failed one both
        // carry a real number for the diagnostic to report.
        private long Connect(SMB2Client client)
        {
            var transport = _options.ResolveTransportType();

            if (string.IsNullOrWhiteSpace(_options.Server))
                throw new InvalidOperationException("FileStorage:Smb:Server is not configured.");

            var stopwatch = Stopwatch.StartNew();
            bool connected;

            try
            {
                connected = client.Connect(_options.Server, transport, _options.ConnectTimeoutMs);
            }
            catch (Exception ex)
            {
                // DNS failure and TCP refusal surface as exceptions rather than `false`.
                // Off the ministry VPN the hostname does not resolve at all, and that is
                // expected rather than a config error (spec, Environment).
                throw new SmbException(SmbConstants.StepConnect, status: null,
                    $"Could not reach SMB server '{_options.Server}' over {transport}: {ex.Message}", ex)
                { ElapsedMs = stopwatch.ElapsedMilliseconds };
            }

            if (!connected)
                throw new SmbException(SmbConstants.StepConnect, status: null,
                    $"Could not connect to SMB server '{_options.Server}' over {transport} within {_options.ConnectTimeoutMs}ms.")
                { ElapsedMs = stopwatch.ElapsedMilliseconds };

            return stopwatch.ElapsedMilliseconds;
        }

        private long Login(SMB2Client client)
        {
            var method = _options.ResolveAuthenticationMethod();
            var stopwatch = Stopwatch.StartNew();

            var status = client.Login(_options.Domain, _options.Username, _options.Password, method);

            if (status != NTStatus.STATUS_SUCCESS)
                throw new SmbException(SmbConstants.StepLogin, status,
                    $"SMB login as '{_options.Username}' in domain '{_options.Domain}' using {method} failed with {status}.")
                { ElapsedMs = stopwatch.ElapsedMilliseconds };

            return stopwatch.ElapsedMilliseconds;
        }

        private async Task<IDisposable> AcquireSlotAsync(CancellationToken cancellationToken)
        {
            if (!await _concurrency.WaitAsync(SmbConstants.SessionAcquireTimeoutMs, cancellationToken))
                throw new SmbException(SmbConstants.StepConnect, status: null,
                    $"No SMB session slot became available within {SmbConstants.SessionAcquireTimeoutMs}ms " +
                    $"(FileStorage:Smb:MaxConcurrentSessions = {_options.MaxConcurrentSessions}).");

            return new SemaphoreSlot(_concurrency);
        }

        private void TryDisconnect(SMB2Client? client)
        {
            if (client == null)
                return;

            try
            {
                client.Disconnect();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disconnecting a partially-established SMB client.");
            }
        }

        public void Dispose() => _concurrency.Dispose();

        // Ties a semaphore slot to the lifetime of the session that holds it, so a
        // download that is abandoned mid-stream gives its slot back when ASP.NET
        // disposes the response stream.
        private sealed class SemaphoreSlot : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;
            private bool _released;

            public SemaphoreSlot(SemaphoreSlim semaphore) => _semaphore = semaphore;

            public void Dispose()
            {
                if (_released)
                    return;

                _released = true;
                _semaphore.Release();
            }
        }
    }
}
