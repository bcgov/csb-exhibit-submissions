namespace CES.Business.Models
{
    public class SubmissionTicketModel
    {
        public string AppearanceId { get; set; } = null!;
        public string? AppearanceDateTime { get; set; }
        public string? AppearanceSequenceNumber { get; set; }
        public string? AppearanceReasonCode { get; set; }
        public string? CourtListType { get; set; }
        public string FileNumberText { get; set; } = null!;
        public string? AccusedName { get; set; }
        public string? AccusedDOB { get; set; }
    }
}
