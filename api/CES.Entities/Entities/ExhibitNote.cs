using CES.Entities.Infrastructure;

namespace CES.Entities
{
    // Registry-only note attached to an exhibit (CES-38 extension). Append-only and
    // immutable once saved: there is no update or delete path. Kept separate from the
    // SubmissionAuditLog change history — these notes are protected for JJ/registry use
    // and never surface in the exhibit's field-change history.
    public class ExhibitNote
    {
        public int Id { get; set; }
        public Guid FileId { get; set; }
        public StoredFiles File { get; set; } = null!;
        public string NoteText { get; set; } = null!;
        // FK to ApplicationUser.Id — see BaseEntity.CreatedByUserId.
        public int? CreatedByUserId { get; set; }
        public ApplicationUser? CreatedByUser { get; set; }
        public DateTime CreatedAtUTC { get; set; } = SystemDate.UtcNow();
    }
}
