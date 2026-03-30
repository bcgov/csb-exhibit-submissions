using CES.Entities.Infrastructure;

namespace CES.Entities
{
    public class Submission:BaseEntity
    {
        public DateTime? UploadDate { get; set; }
        public string AppearanceID { get; set; } = string.Empty;
        public DateTime? AppearanceDateTime { get; set; }
        public string CourtListType { get; set; } = string.Empty;
        public string FileNumberText { get; set; } = string.Empty;
        public string LocationId { get; set; } = string.Empty;
        public string LocationNameText { get; set; } = string.Empty;
        public string RoomCode { get; set; } = string.Empty;
        public string RoomText { get; set; } = string.Empty;
        public string AccusedName { get; set; } = string.Empty;
        public string AccusedDOB { get; set; } = string.Empty;
        public string OfficerNumber {get;set;} = string.Empty;
        public List<StoredFiles> Files {get;set;} = new List<StoredFiles>();

        
        public Submission()
        {
            UploadDate = SystemDate.UtcNow();
        }
    }
}