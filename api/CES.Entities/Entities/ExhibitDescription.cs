using CES.Entities.Infrastructure;

namespace CES.Entities
{
    // One description entry for an exhibit (CES-42). Append-only and immutable once
    // saved: there is no update or delete path — a correction or expansion is a new
    // entry, and the earlier entries remain as the description's history. Unlike
    // ExhibitNote these are not registry-only; officers see and add them too.
    public class ExhibitDescription
    {
        public int Id { get; set; }
        public Guid FileId { get; set; }
        public StoredFiles File { get; set; } = null!;
        public string DescriptionText { get; set; } = null!;
        public string? CreatedBy { get; set; }
        public DateTime CreatedAtUTC { get; set; } = SystemDate.UtcNow();
    }
}
