using CES.Business.Models;
using CES.Entities;

namespace CES.Business.Interfaces
{
    public interface IFileService
    {
        Task<StoredFiles?> RetrieveFileMetaData(Guid fileId);
        Task<SubmissionFile> MarkExhibitAsync(Guid fileId, string markedValue, string changedBy, bool isAdminOverride = false);
        Task<SubmissionFile> EnterExhibitAsync(Guid fileId, string enteredValue, string changedBy, bool isAdminOverride = false);
        Task<SubmissionFile> UpdateExhibitDescriptionAsync(Guid fileId, string description, string changedBy, bool isAdminOverride = false);
        Task<List<ExhibitHistoryEntryModel>> GetExhibitHistoryAsync(Guid fileId);
    }
}
