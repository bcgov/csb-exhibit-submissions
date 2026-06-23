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

        // Classification fields (added for CES-28)
        public string? MarkedValue { get; set; }
        public DateTime? MarkedAt { get; set; }
        public string? EnteredValue { get; set; }
        public DateTime? EnteredAt { get; set; }
        public string? Description { get; set; }

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