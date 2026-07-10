using CES.Business.Models;
using CES.Entities;

namespace CES.Business.Interfaces
{
    public interface IFileService
    {
        Task<StoredFiles?> RetrieveFileMetaData(Guid fileId);
        // Opens an exhibit's bytes for view/download, resolving accepted files from
        // the canonical store and pending files from the temporary store (CES-39).
        Task<(Stream? stream, string? fileName, string? contentType, string? error)> GetExhibitContentAsync(Guid fileId);
        Task<SubmissionFile> MarkExhibitAsync(Guid fileId, string markedValue, string changedBy, bool isAdminOverride = false);
        Task<SubmissionFile> EnterExhibitAsync(Guid fileId, string enteredValue, string changedBy, bool isAdminOverride = false);
        Task<SubmissionFile> UpdateExhibitDescriptionAsync(Guid fileId, string description, string changedBy, bool isAdminOverride = false);
        Task<SubmissionFile> UpdateExhibitEvidenceSourceAsync(Guid fileId, string? evidenceSourceType, string changedBy, bool isAdminOverride = false);
        Task<List<ExhibitHistoryEntryModel>> GetExhibitHistoryAsync(Guid fileId);
        // Registry-only notes (CES-38 extension). Append-only: add creates an immutable
        // note; there is deliberately no update or delete.
        Task<List<ExhibitNoteModel>> GetExhibitNotesAsync(Guid fileId);
        Task<ExhibitNoteModel> AddExhibitNoteAsync(Guid fileId, string noteText, string createdBy);
    }
}
