using CES.Business.Constants;
using CES.Entities.Enums;

namespace CES.Business.Models
{
    public class SubmissionListFilter
    {
        public DateTime? SubmissionDateFrom { get; set; }
        public DateTime? SubmissionDateTo { get; set; }
        public string? FileNumberText { get; set; }
        public string? AccusedName { get; set; }
        public SubmissionStatus? Status { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = PagingConstants.DefaultPageSize;
    }
}
