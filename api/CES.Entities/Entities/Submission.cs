using CES.Entities.Enums;
using CES.Entities.Infrastructure;

namespace CES.Entities
{
    public class Submission : BaseEntity
    {
        public DateTime? UploadDate { get; set; }
        // Court appearance date (yyyymmdd) — also used as the storage-path date segment.
        public string ShortDate { get; set; } = string.Empty;
        // Full court appearance date-time (carries the appearance time) from the court-search context.
        public string? AppearanceDateTime { get; set; }
        public string LocationId { get; set; } = string.Empty;
        public string? LocationNameText { get; set; } = string.Empty;
        public string RoomCode { get; set; } = string.Empty;
        public string? RoomText { get; set; } = string.Empty;
        public string? OfficerNumber { get; set; } = string.Empty;
        public List<StoredFiles> Files { get; set; } = new List<StoredFiles>();
        public ICollection<SubmissionTicket> Tickets { get; set; } = new List<SubmissionTicket>();
        public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;
        public DateTime? StatusChangedDateUTC { get; set; }

        public Submission()
        {
            UploadDate = SystemDate.UtcNow();
        }
    }
}
