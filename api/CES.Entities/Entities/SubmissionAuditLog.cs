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
        // FK to ApplicationUser.Id — see BaseEntity.CreatedByUserId. The navigation is
        // what read models and the metadata sidecar resolve the actor's email through.
        public int? ChangedByUserId { get; set; }
        public ApplicationUser? ChangedByUser { get; set; }
        public DateTime ChangedAtUTC { get; set; } = SystemDate.UtcNow();
    }
}
