using CES.Business.Models;

namespace CES.Business.Interfaces
{
    public interface ISubmissionService
    {
        public Task<bool> SubmitEvidence(EvidenceSubmissionModel model);
        public Task<SubmissionReviewModel?> RetrieveSubmission(int submissionId);
        public Task<List<SubmissionReviewModel>> RetrieveSubmissionListing();
        public Task<bool> AcceptSubmissions(EvidenceAcceptanceModel model);
    }
}
