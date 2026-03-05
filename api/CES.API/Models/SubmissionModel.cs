using CES.Business.Models;

namespace CES.API.Models
{
    public class SubmissionModel : EvidenceSubmissionModel
    {
        public List<IFormFile> Files { get; set; } = new();
    }
}