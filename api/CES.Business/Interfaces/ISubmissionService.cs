using CES.Business.Models;

namespace CES.Business.Interfaces
{
    public interface ISubmissionService
    {
        // submittedByUserId / removedByUserId are ApplicationUser.Id values; null records an
        // unattributed action rather than failing it.
        Task<int?> SubmitEvidence(EvidenceSubmissionModel model, int? submittedByUserId);
        Task<SubmissionReviewModel?> RetrieveSubmission(int submissionId);
        Task<PagedResult<SubmissionReviewModel>> RetrieveSubmissionListing(SubmissionListFilter filter);
        Task<(bool success, string? error)> RejectSubmissions(SubmissionActionModel model);
        Task<List<PriorSubmissionModel>> GetSubmissionsByFileNumberAsync(string fileNumberText);
        Task<List<ExhibitSearchResultModel>> SearchExhibitsAsync(ExhibitSearchFilter filter);
        Task<bool> RemoveFileAsync(Guid fileId, int? removedByUserId);
    }
}
