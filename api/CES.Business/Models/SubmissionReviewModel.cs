using System;
using System.Collections.Generic;

namespace CES.Business.Models
{
    public class SubmissionReviewModel
    {
        public int Id { get; set; }
        public DateTime? SubmissionDate { get; set; }
        public string CourtDateTime { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public DateTime? StatusChangedDate { get; set; }
        public int ExhibitCount { get; set; }
        public List<SubmissionTicketModel> Tickets { get; set; } = new();
        public List<SubmissionFile> Files { get; set; } = new List<SubmissionFile>();
    }

    public class SubmissionFile
    {
        public Guid Id { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string StorageProvider { get; set; } = string.Empty;
        public string Status { get; set; } = "Unclassified";
        public string? MarkedValue { get; set; }
        public DateTime? MarkedAt { get; set; }
        public string? EnteredValue { get; set; }
        public DateTime? EnteredAt { get; set; }
        // Append-only description entries, oldest → newest (CES-42).
        public List<ExhibitDescriptionEntryModel> Descriptions { get; set; } = new();
        public string? EvidenceSourceType { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}
