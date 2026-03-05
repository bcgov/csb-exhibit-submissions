namespace CES.Business.Models
{
    public class EvidenceSubmissionModel
    {
        public DateTime Date { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public string TicketNumber { get; set; } = string.Empty;
        public string DisputantName { get; set; } = string.Empty;
        public string OfficerNumber { get; set; } = string.Empty;

        public List<FileUpload> fileUploads {get;set;} = new List<FileUpload>();
    }
}