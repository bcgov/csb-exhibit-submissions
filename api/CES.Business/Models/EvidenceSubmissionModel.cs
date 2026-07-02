using System.ComponentModel.DataAnnotations;

namespace CES.Business.Models
{
    public class EvidenceSubmissionModel
    {
        public required string ShortDate { get; set; }
        // Full court appearance date-time (carries the appearance time) from the court-search context.
        public string? AppearanceDateTime { get; set; }
        // When set, files are appended to this existing Pending submission instead of creating a new one.
        public int? SubmissionId { get; set; }
        public string LocationId { get; set; } = string.Empty;
        public string? LocationNameText { get; set; }
        public string RoomCode { get; set; } = string.Empty;
        public string? RoomText { get; set; }
        public string? OfficerNumber { get; set; }

        [MinLength(1, ErrorMessage = "At least one ticket is required.")]
        public required List<SubmissionTicketModel> Tickets { get; set; }

        public List<FileUpload> fileUploads { get; set; } = new List<FileUpload>();
    }
}
