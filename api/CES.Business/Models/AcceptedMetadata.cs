namespace CES.Business.Models
{
    // Serialized shape of the per-submission metadata.json sidecar (CES-39).
    // The DB is the source of truth; this is a derived export, fully regenerable.
    public class AcceptedMetadata
    {
        public int SchemaVersion { get; set; }
        public int SubmissionId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? AcceptedAtUTC { get; set; }
        public DateTime? LastUpdatedUTC { get; set; }
        public string HashAlgorithm { get; set; } = string.Empty;
        public List<AcceptedMetadataTicket> Tickets { get; set; } = new();
        public List<AcceptedMetadataExhibit> Exhibits { get; set; } = new();
        public List<AcceptedMetadataRevision> Revisions { get; set; } = new();
    }

    public class AcceptedMetadataTicket
    {
        public string AppearanceId { get; set; } = string.Empty;
        public string FileNumberText { get; set; } = string.Empty;
        public string? AccusedName { get; set; }
    }

    public class AcceptedMetadataExhibit
    {
        public Guid ExhibitId { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        // Single physical location, relative to AcceptedPath.
        public string? CanonicalPath { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string? Sha256 { get; set; }
        public bool IsAccepted { get; set; }
        public DateTime? AcceptedAtUTC { get; set; }
        public string? MarkedValue { get; set; }
        public DateTime? MarkedAt { get; set; }
        public string? EnteredValue { get; set; }
        public DateTime? EnteredAt { get; set; }
        // Full append-only description history, oldest → newest (CES-42).
        public List<AcceptedMetadataDescription> Descriptions { get; set; } = new();
        public string? EvidenceSourceType { get; set; }
        // De-dup: one file, many tickets — the full ticket mapping for traceability.
        public List<string> AssociatedTickets { get; set; } = new();
    }

    public class AcceptedMetadataDescription
    {
        public string Text { get; set; } = string.Empty;
        /// <summary>Actor's email address — see the note on <c>AcceptedMetadataWriter</c>.</summary>
        public string? By { get; set; }
        public DateTime AtUTC { get; set; }
    }

    public class AcceptedMetadataRevision
    {
        public DateTime AtUTC { get; set; }
        /// <summary>Actor's email address — see the note on <c>AcceptedMetadataWriter</c>.</summary>
        public string? By { get; set; }
        public string Change { get; set; } = string.Empty;
    }
}
