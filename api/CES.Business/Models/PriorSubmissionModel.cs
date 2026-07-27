namespace CES.Business.Models
{
    public class PriorSubmissionModel
    {
        public int SubmissionId { get; set; }
        public DateTime? SubmissionDate { get; set; }
        public string? AppearanceDateTime { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public List<SubmissionFile> Files { get; set; } = new();
    }
}
