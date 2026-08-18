namespace CES.API.FileStorage.Smb
{
    // The single seam between the SMB code and the network, so tests and the Stage 1
    // diagnostic can both work against a substitute.
    //
    // Two entry points because the diagnostic genuinely needs the half-built form: with
    // the share name unknown, ListShares runs on a logged-in session that has not been
    // tree-connected, and that is how the share name gets discovered.
    public interface ISmbSessionFactory
    {
        // Connect + login. The caller tree-connects. Throws SmbException carrying the
        // failing step and NTStatus.
        Task<SmbSession> ConnectAsync(CancellationToken cancellationToken = default);

        // Connect + login + tree-connect to FileStorage:Smb:ShareName. The production
        // entry point — everything outside the diagnostic uses this.
        Task<SmbSession> OpenShareAsync(CancellationToken cancellationToken = default);
    }
}
