namespace CES.Entities
{
    public class SubmissionTicket
    {
        public int Id { get; set; }
        public int SubmissionId { get; set; }
        public Submission Submission { get; set; } = null!;
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
