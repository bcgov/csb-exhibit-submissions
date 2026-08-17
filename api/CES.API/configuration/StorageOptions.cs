using CES.Business.Constants;

namespace CES.API
{
    public interface IStorageOptions
    {
        public string PendingProvider { get; }
        public string AcceptedProvider { get; }
        public string LocalPath { get; }
        public string AcceptedPath { get; }
        public long MaxFileSize { get; }
    }

    public class StorageOptions : IStorageOptions
    {
        // The two halves of the store are chosen independently: uploads want fast
        // local disk, the accepted store wants a managed system of record. A single
        // "Provider" could not express that pairing, so it was replaced by these two
        // (see FileStorageRegistration for the legacy-key guard).
        public string PendingProvider { get; set; } = FileStorageProviders.Local;
        public string AcceptedProvider { get; set; } = FileStorageProviders.Local;

        // Root for pending uploads. Used when PendingProvider = Local.
        public string LocalPath { get; set; } = "uploads";

        // Root for the accepted store. Used when AcceptedProvider = Local.
        public string AcceptedPath { get; set; } = "accepted";

        public long MaxFileSize { get; set; } = 104857600; // 100MB
    }
}
