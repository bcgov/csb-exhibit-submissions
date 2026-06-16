using CES.Entities.Infrastructure;

namespace CES.Entities
{
    public class SubmissionAuditLog
    {
        public int Id { get; set; }
        public int SubmissionId { get; set; }
        public Submission Submission { get; set; } = null!;
        public Guid? FileId { get; set; }
        public StoredFiles? File { get; set; }
        public string FieldName { get; set; } = null!;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? ChangedBy { get; set; }
        public DateTime ChangedAtUTC { get; set; } = SystemDate.UtcNow();
    }
}
