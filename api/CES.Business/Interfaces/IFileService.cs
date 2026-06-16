using CES.Business.Models;
using CES.Entities;

namespace CES.Business.Interfaces
{
    public interface IFileService
    {
        Task<StoredFiles?> RetrieveFileMetaData(Guid fileId);
        Task<SubmissionFile> MarkExhibitAsync(Guid fileId, string markedValue, string changedBy);
        Task<SubmissionFile> EnterExhibitAsync(Guid fileId, string enteredValue, string changedBy);
        Task<SubmissionFile> UpdateExhibitDescriptionAsync(Guid fileId, string description, string changedBy);
    }
}
