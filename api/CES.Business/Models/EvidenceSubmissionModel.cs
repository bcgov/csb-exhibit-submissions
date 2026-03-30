using CES.Business.Models.Location;

namespace CES.Business.Models
{
    public class EvidenceSubmissionModel : CourtList
    {
        public string OfficerNumber { get; set; } = string.Empty;

        public List<FileUpload> fileUploads {get;set;} = new List<FileUpload>();
    }
}