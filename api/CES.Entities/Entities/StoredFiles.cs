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

        // FKs to ApplicationUser.Id — see BaseEntity.CreatedByUserId for why the audit
        // columns hold an id rather than a name. StoredFiles does not derive from
        // BaseEntity (its key is a Guid), so the same pair is declared here.
        public int? CreatedByUserId { get; set; }
        public ApplicationUser? CreatedByUser { get; set; }
        public int? UpdatedByUserId { get; set; }
        public ApplicationUser? UpdatedByUser { get; set; }
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

        // Append-only description entries, oldest first (CES-42). Replaces the former
        // single mutable Description string.
        public ICollection<ExhibitDescription> Descriptions { get; set; } = new List<ExhibitDescription>();

        // Evidence source device — "BodyCam" / "DashCam" / "Other"; null = unset (added for CES-18)
        public string? EvidenceSourceType { get; set; }

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

        /// <param name="updatedByUserId">
        /// The acting user's <c>ApplicationUser.Id</c>, or null for a system-driven update.
        /// </param>
        public void SetUpdateBy(int? updatedByUserId = null)
        {
            UpdatedByUserId = updatedByUserId;
            UpdatedDateUTC = SystemDate.UtcNow();
        }

    }
}