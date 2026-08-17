using CES.Business.Models;
using CES.Entities;

namespace CES.Business.Interfaces
{
    // The pending (pre-acceptance) half of the file store: where an officer's upload
    // lands and lives until the exhibit is classified and promoted.
    //
    // Split out of IFileStorage so the pending and accepted halves can be backed by
    // different providers, chosen independently via FileStorage:PendingProvider and
    // FileStorage:AcceptedProvider. Consumers do not depend on this interface —
    // they keep using IFileStorage, which FileStorageCoordinator composes from the
    // two halves.
    public interface IPendingFileStore
    {
        // Persists an upload under {storagePath} and returns the row describing it.
        // Sets StoredFiles.StorageProvider to the implementation's provider id.
        Task<StoredFiles> SaveAsync(FileUpload file, string storagePath);

        // Opens the pending bytes for reading. Throws FileNotFoundException if absent.
        Task<Stream> GetAsync(StoredFiles storedFile);

        // Removes the pending bytes. Throws FileNotFoundException if absent — callers
        // that tolerate a missing file check ExistsAsync first.
        Task DeleteAsync(StoredFiles storedFile);

        // Whether the pending bytes are still present. Lets the coordinator tell
        // "already cleaned up" apart from "something is wrong" without opening a
        // stream or catching an exception for control flow.
        Task<bool> ExistsAsync(StoredFiles storedFile);
    }
}
