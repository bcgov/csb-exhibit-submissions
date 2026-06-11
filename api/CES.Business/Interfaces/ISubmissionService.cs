using CES.Business.Models;

namespace CES.Business.Interfaces
{
    public interface ISubmissionService
    {
        Task<bool> SubmitEvidence(EvidenceSubmissionModel model);
        Task<SubmissionReviewModel?> RetrieveSubmission(int submissionId);
        Task<List<SubmissionReviewModel>> RetrieveSubmissionListing();
        Task<bool> AcceptSubmissions(EvidenceAcceptanceModel model);
        Task<bool> RejectSubmissions(EvidenceAcceptanceModel model);
        Task<List<PriorSubmissionModel>> GetSubmissionsByFileNumberAsync(string fileNumberText);
        Task<bool> RemoveFileAsync(Guid fileId);
    }
}
