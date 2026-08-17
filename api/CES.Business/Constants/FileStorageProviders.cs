namespace CES.Business.Constants
{
    // Provider identifiers for the two independently-configurable halves of the file
    // store (pending uploads and the accepted store). These strings are both the
    // configuration values (FileStorage:PendingProvider / FileStorage:AcceptedProvider)
    // and the value stamped on StoredFiles.StorageProvider at upload, so they are
    // named here rather than repeated inline (project rule: no magic values).
    public static class FileStorageProviders
    {
        // Pod-local disk, under FileStorage:LocalPath / FileStorage:AcceptedPath.
        public const string Local = "Local";

        // Remote SMB share. Accepted store only — see spec/smb-file-storage.md.
        public const string Smb = "Smb";
    }
}
