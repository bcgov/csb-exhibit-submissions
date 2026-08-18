using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using SMBLibrary;
// System.IO.FileAttributes is in the implicit usings and would otherwise win.
using SmbFileAttributes = SMBLibrary.FileAttributes;

namespace CES.API.FileStorage.Smb
{
    // Walks the SMB stack one step at a time and reports each outcome, so a single call
    // tells us which of the open unknowns is wrong. Every step is caught: the endpoint
    // answers 200 with a failed step rather than throwing, because "how far did we get"
    // is the entire product here.
    public sealed class SmbDiagnosticsService : ISmbDiagnosticsService
    {
        private readonly ISmbSessionFactory _sessionFactory;
        private readonly SmbOptions _options;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<SmbDiagnosticsService> _logger;

        public SmbDiagnosticsService(
            ISmbSessionFactory sessionFactory,
            IOptions<SmbOptions> options,
            IHostEnvironment environment,
            ILogger<SmbDiagnosticsService> logger)
        {
            _sessionFactory = sessionFactory;
            _options = options.Value;
            _environment = environment;
            _logger = logger;
        }

        // Available in Development by default, and elsewhere only when explicitly
        // switched on — DiagnosticsEnabled exists so the check can be run against a
        // deployed environment without also putting it in Development mode.
        public bool IsEnabled => _environment.IsDevelopment() || _options.DiagnosticsEnabled;

        public async Task<SmbHealthResponse> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            var response = new SmbHealthResponse();
            var overall = Stopwatch.StartNew();

            SmbSession? session = null;

            try
            {
                session = await TryConnectAndLoginAsync(response, cancellationToken);

                if (session != null)
                {
                    response.Negotiated = session.Limits;

                    // ListShares can fail on a hardened server while share access works
                    // fine, so its outcome never gates the tree connect.
                    RunListShares(session, response.Steps.ListShares);

                    if (RunTreeConnect(session, response.Steps.TreeConnect))
                    {
                        if (RunListBasePath(session, response.Steps.ListBasePath))
                            RunProbeRead(session, response.Steps.ProbeRead);
                        else
                            response.Steps.ProbeRead.Skipped = $"Skipped: {SmbConstants.StepListBasePath} did not succeed.";
                    }
                    else
                    {
                        response.Steps.ListBasePath.Skipped = $"Skipped: {SmbConstants.StepTreeConnect} did not succeed.";
                        response.Steps.ProbeRead.Skipped = $"Skipped: {SmbConstants.StepTreeConnect} did not succeed.";
                    }
                }
            }
            finally
            {
                session?.Dispose();
                response.ElapsedMs = overall.ElapsedMilliseconds;
            }

            return response;
        }

        // ConnectAsync does connect + login together, so the two steps are separated
        // here from the session's own timings, or from the failing step on the exception.
        private async Task<SmbSession?> TryConnectAndLoginAsync(SmbHealthResponse response, CancellationToken cancellationToken)
        {
            var connect = response.Steps.Connect;
            var login = response.Steps.Login;

            login.Domain = _options.Domain;
            login.Method = _options.AuthenticationMethod;

            try
            {
                var session = await _sessionFactory.ConnectAsync(cancellationToken);

                connect.Ok = true;
                connect.ElapsedMs = session.ConnectElapsedMs;

                login.Ok = true;
                login.Status = nameof(NTStatus.STATUS_SUCCESS);
                login.ElapsedMs = session.LoginElapsedMs;

                return session;
            }
            catch (SmbException ex)
            {
                _logger.LogWarning(ex, "SMB diagnostic failed at step {Step} with status {Status}.", ex.Step, ex.StatusName);

                var failed = ex.Step == SmbConstants.StepLogin ? login : connect;
                Fail(failed, ex.Status, ex.Message);
                failed.ElapsedMs = ex.ElapsedMs;

                if (ex.Step == SmbConstants.StepLogin)
                {
                    // The transport got us far enough to be told no.
                    connect.Ok = true;
                }
                else
                {
                    login.Skipped = $"Skipped: {SmbConstants.StepConnect} did not succeed.";
                }

                MarkRemainderSkipped(response, ex.Step);
                return null;
            }
            catch (Exception ex)
            {
                // A misconfigured TransportType/AuthenticationMethod throws before any
                // network call — still a connect-step failure from the caller's view.
                _logger.LogWarning(ex, "SMB diagnostic could not establish a session.");
                Fail(connect, status: null, ex.Message);
                MarkRemainderSkipped(response, SmbConstants.StepConnect);
                return null;
            }
        }

        private void RunListShares(SmbSession session, SmbListSharesStep step)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var shares = session.ListShares(out var status);

                step.Status = status.ToString();
                step.Ok = status == NTStatus.STATUS_SUCCESS;
                step.Shares = shares;

                if (!step.Ok)
                    step.Error = $"Share enumeration returned {status}. This goes through IPC$/SRVSVC and may be denied " +
                                 "on a hardened server even when share access itself works.";
            }
            catch (Exception ex)
            {
                Fail(step, status: null, ex.Message);
            }
            finally
            {
                step.ElapsedMs = stopwatch.ElapsedMilliseconds;
            }
        }

        private bool RunTreeConnect(SmbSession session, SmbTreeConnectStep step)
        {
            step.Share = _options.ShareName;

            if (string.IsNullOrWhiteSpace(_options.ShareName))
            {
                step.Skipped = "FileStorage:Smb:ShareName is not configured — read the share name out of steps.listShares.shares.";
                return false;
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var status = session.TryTreeConnect(_options.ShareName);

                step.Status = status.ToString();
                step.Ok = status == NTStatus.STATUS_SUCCESS;

                if (!step.Ok)
                    step.Error = $"Tree connect to share '{_options.ShareName}' returned {status}.";

                return step.Ok;
            }
            catch (Exception ex)
            {
                Fail(step, status: null, ex.Message);
                return false;
            }
            finally
            {
                step.ElapsedMs = stopwatch.ElapsedMilliseconds;
            }
        }

        // Lists BasePath — or the share root when BasePath is empty, which is how the
        // base path itself gets discovered. Listing happens only here; production paths
        // are exact and never enumerate.
        private bool RunListBasePath(SmbSession session, SmbListPathStep step)
        {
            var basePath = SmbPath.Normalize(_options.BasePath);
            step.BasePath = basePath;

            var stopwatch = Stopwatch.StartNew();
            object? handle = null;

            try
            {
                var status = session.FileStore.CreateFile(
                    out handle,
                    out _,
                    basePath,
                    AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
                    SmbFileAttributes.Directory,
                    ShareAccess.Read | ShareAccess.Write,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT,
                    securityContext: null);

                step.Status = status.ToString();

                if (status != NTStatus.STATUS_SUCCESS)
                {
                    step.Error = basePath.Length == 0
                        ? $"Opening the share root returned {status}."
                        : $"Opening base path '{basePath}' returned {status}.";
                    return false;
                }

                status = session.FileStore.QueryDirectory(
                    out var entries, handle, SmbConstants.DirectorySearchPattern, FileInformationClass.FileDirectoryInformation);

                step.Status = status.ToString();

                if (status != NTStatus.STATUS_SUCCESS)
                {
                    step.Error = $"Listing '{step.BasePath}' returned {status}.";
                    return false;
                }

                var names = entries
                    .OfType<FileDirectoryInformation>()
                    .Select(entry => entry.FileName)
                    .Where(name => name is not (SmbConstants.CurrentDirectoryEntry or SmbConstants.ParentDirectoryEntry))
                    .ToList();

                step.Truncated = names.Count > SmbConstants.MaxDiagnosticDirectoryEntries;
                step.Entries = names.Take(SmbConstants.MaxDiagnosticDirectoryEntries).ToList();
                step.Ok = true;

                return true;
            }
            catch (Exception ex)
            {
                Fail(step, status: null, ex.Message);
                return false;
            }
            finally
            {
                CloseQuietly(session, handle);
                step.ElapsedMs = stopwatch.ElapsedMilliseconds;
            }
        }

        // Reads FileStorage:Smb:ProbeFile end to end and hashes it, proving the read path
        // — not just the connection — before Stage 2 depends on it.
        private void RunProbeRead(SmbSession session, SmbProbeReadStep step)
        {
            if (string.IsNullOrWhiteSpace(_options.ProbeFile))
            {
                step.Skipped = "FileStorage:Smb:ProbeFile is not configured — set it to a small file under BasePath to exercise the read path.";
                return;
            }

            var path = SmbPath.Combine(_options.BasePath, _options.ProbeFile);
            step.Path = path;

            var stopwatch = Stopwatch.StartNew();
            object? handle = null;

            try
            {
                var status = session.FileStore.CreateFile(
                    out handle,
                    out _,
                    path,
                    AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
                    SmbFileAttributes.Normal,
                    ShareAccess.Read,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT,
                    securityContext: null);

                step.Status = status.ToString();

                if (status != NTStatus.STATUS_SUCCESS)
                {
                    step.Error = $"Opening probe file '{path}' returned {status}.";
                    return;
                }

                var (bytes, sha256, readStatus) = ReadAndHash(session, handle);

                step.Status = readStatus.ToString();
                step.Bytes = bytes;
                step.Sha256 = sha256;
                step.Ok = true;

                if (bytes >= SmbConstants.MaxProbeReadBytes)
                    step.Error = $"Probe stopped at the {SmbConstants.MaxProbeReadBytes}-byte cap; the hash covers only the bytes read.";
            }
            catch (Exception ex)
            {
                Fail(step, status: null, ex.Message);
            }
            finally
            {
                CloseQuietly(session, handle);
                step.ElapsedMs = stopwatch.ElapsedMilliseconds;
            }
        }

        private (long Bytes, string Sha256, NTStatus Status) ReadAndHash(SmbSession session, object handle)
        {
            // The negotiated MaxReadSize is a hard ceiling: asking for more than the
            // server agreed to is rejected, not silently trimmed.
            var chunkSize = (int)Math.Min(
                (uint)Math.Max(1, _options.BufferSize),
                session.Limits.MaxReadSize);

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long offset = 0;
            var status = NTStatus.STATUS_SUCCESS;

            while (offset < SmbConstants.MaxProbeReadBytes)
            {
                var request = (int)Math.Min(chunkSize, SmbConstants.MaxProbeReadBytes - offset);

                status = session.FileStore.ReadFile(out var data, handle, offset, request);

                if (status == NTStatus.STATUS_END_OF_FILE)
                    break;

                if (status != NTStatus.STATUS_SUCCESS)
                    throw new SmbException(SmbConstants.StepProbeRead, status,
                        $"Reading the probe file at offset {offset} returned {status}.");

                // A zero-length success is also end-of-file on some servers.
                if (data == null || data.Length == 0)
                    break;

                hash.AppendData(data);
                offset += data.Length;
            }

            return (offset, Convert.ToHexString(hash.GetHashAndReset()), status);
        }

        private void CloseQuietly(SmbSession session, object? handle)
        {
            if (handle == null)
                return;

            try
            {
                session.FileStore.CloseFile(handle);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing an SMB handle opened by the diagnostic.");
            }
        }

        private static void Fail(SmbHealthStep step, NTStatus? status, string error)
        {
            step.Ok = false;
            step.Status = status?.ToString();
            step.Error = error;
        }

        private static void MarkRemainderSkipped(SmbHealthResponse response, string failedStep)
        {
            var reason = $"Skipped: {failedStep} did not succeed.";

            response.Steps.ListShares.Skipped = reason;
            response.Steps.TreeConnect.Skipped = reason;
            response.Steps.ListBasePath.Skipped = reason;
            response.Steps.ProbeRead.Skipped = reason;
        }
    }
}
