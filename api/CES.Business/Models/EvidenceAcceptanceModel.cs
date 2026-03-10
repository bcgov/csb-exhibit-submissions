namespace CES.Business.Models
{
    public class EvidenceAcceptanceModel
    {
        public int FileId { get; set; }
        public List<Guid> acceptedFiles {get;set;} = new List<Guid>();
    }
}