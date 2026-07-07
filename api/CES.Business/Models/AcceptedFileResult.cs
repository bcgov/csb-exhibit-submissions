namespace CES.Business.Models
{
    // Result of promoting a pending file into the accepted store (CES-39).
    public class AcceptedFileResult
    {
        // Path relative to AcceptedPath, e.g.
        // {locationId}/{roomCode}/{shortDate}/{submissionId}/{exhibitId}{ext}.
        public string CanonicalPath { get; set; } = string.Empty;
        public string AcceptedFileName { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
    }
}
