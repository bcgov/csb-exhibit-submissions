using CES.Entities.Infrastructure;

namespace CES.Entities
{
    public class Submission:BaseEntity
    {
        public DateTime? UploadDate { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public string TicketNumber { get; set; }  = string.Empty;
        public string DisputantName {get;set;} = string.Empty;
        public string OfficerNumber {get;set;} = string.Empty;
        public List<StoredFiles> Files {get;set;} = new List<StoredFiles>();

        
        public Submission()
        {
            UploadDate = SystemDate.UtcNow();
        }
    }
}