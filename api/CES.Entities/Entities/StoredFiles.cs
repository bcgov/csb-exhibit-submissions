using CES.Entities.Infrastructure;

namespace CES.Entities
{
    public class StoredFiles
    {
        public Guid Id { get; set; } = new Guid();
        public string OriginalFileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
        public string StoredPath { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string StorageProvider { get; set; } = string.Empty;
        public DateTime CreatedDateUTC { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedDateUTC { get; set; }
        public bool IsDeleted { get; set; } = false;

        // FK to parent Submission (explicit property — EF previously managed as shadow FK)
        public int SubmissionId { get; set; }
        public Submission Submission { get; set; } = null!;

        public DateTime? DeletedAtUTC { get; set; }

        // Classification fields (added for CES-28)
        public string? MarkedValue { get; set; }
        public DateTime? MarkedAt { get; set; }
        public string? EnteredValue { get; set; }
        public DateTime? EnteredAt { get; set; }
        public string? Description { get; set; }

        // Per-file acceptance / canonical storage (added for CES-39).
        // The DB is the source of truth; metadata.json is a derived export.
        public bool IsAccepted { get; set; } = false;
        public DateTime? AcceptedAtUTC { get; set; }
        // Path relative to StorageOptions.AcceptedPath, e.g.
        // {locationId}/{roomCode}/{shortDate}/{submissionId}/{exhibitId}{ext}.
        public string? CanonicalPath { get; set; }
        // SHA256 hex captured once at acceptance — the immutability proof.
        public string? Sha256 { get; set; }
        // The {exhibitId}{ext} leaf under the submission folder.
        public string? AcceptedFileName { get; set; }

        public StoredFiles()
        {
            CreatedDateUTC = SystemDate.UtcNow();
        }

        public void SetUpdateBy(string updator = "System")
        {
            UpdatedBy = updator;
            UpdatedDateUTC = SystemDate.UtcNow();
        }

    }
}