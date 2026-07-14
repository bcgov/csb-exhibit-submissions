namespace CES.Business.Constants
{
    // Constants for the accepted-exhibit file store (CES-39).
    // No inline magic values (project rule) — the sidecar schema version, hash
    // algorithm name, and path-sanitization limits all live here.
    public static class AcceptedStorageConstants
    {
        // metadata.json schema version — bump when the serialized shape changes.
        // v2 (CES-42): exhibit.description (string) became exhibit.descriptions (array).
        public const int MetadataSchemaVersion = 2;

        // Hash algorithm recorded in the sidecar and used to compute Sha256.
        public const string HashAlgorithm = "SHA256";

        // The single sidecar file name written per submission folder.
        public const string MetadataFileName = "metadata.json";

        // Temp suffix used for atomic temp+rename writes.
        public const string TempSuffix = ".tmp";

        // Maximum length of any sanitized path segment (defensive bound).
        public const int MaxSegmentLength = 128;
    }
}
