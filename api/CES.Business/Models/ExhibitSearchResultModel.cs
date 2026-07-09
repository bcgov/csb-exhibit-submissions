using System.Collections.Generic;

namespace CES.Business.Models
{
    // One row per non-deleted exhibit (file), carrying its parent submission's
    // context for display. Exhibit-centric — a flat list, not submission-grouped.
    public class ExhibitSearchResultModel
    {
        public SubmissionFile File { get; set; } = new();
        public int SubmissionId { get; set; }
        public DateTime? SubmissionDate { get; set; }
        public string? AppearanceDateTime { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public List<string> FileNumbers { get; set; } = new();
        public string? AccusedName { get; set; }
    }
}
