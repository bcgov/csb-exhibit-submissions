using CES.Business.Models;

namespace CES.Business.Interfaces
{
    public interface ISubmissionService
    {
        public Task<bool> SubmitEvidence(EvidenceSubmissionModel model);
    }
}
