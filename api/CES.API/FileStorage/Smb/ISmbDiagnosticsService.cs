namespace CES.API.FileStorage.Smb
{
    // Stage 1 diagnostic behind GET /api/dev/smb/health. Development-only, admin-only,
    // and never used by a production code path.
    public interface ISmbDiagnosticsService
    {
        // False when the diagnostic is not available in this environment, which is what
        // makes the endpoint 404 rather than run.
        bool IsEnabled { get; }

        Task<SmbHealthResponse> CheckHealthAsync(CancellationToken cancellationToken = default);
    }
}
