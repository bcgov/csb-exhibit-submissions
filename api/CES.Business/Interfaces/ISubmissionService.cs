using CES.Business.Models;

namespace CES.Business.Interfaces
{
    public interface ISubmissionService
    {
        Task<bool> SubmitEvidence(EvidenceSubmissionModel model);
        Task<SubmissionReviewModel?> RetrieveSubmission(int submissionId);
        Task<PagedResult<SubmissionReviewModel>> RetrieveSubmissionListing(SubmissionListFilter filter);
        Task<(bool success, string? error)> AcceptSubmissions(SubmissionActionModel model);
        Task<(bool success, string? error)> RejectSubmissions(SubmissionActionModel model);
        Task<List<PriorSubmissionModel>> GetSubmissionsByFileNumberAsync(string fileNumberText);
        Task<bool> RemoveFileAsync(Guid fileId);
    }
}
