using CES.Business.Models;
using CES.Entities;

namespace CES.Business.Extensions.Entities
{
    public static class StoredFilesExtensions
    {
        public static string DeriveStatus(this StoredFiles f)
        {
            if (f.IsDeleted) return "Removed";
            if (f.EnteredValue != null) return "Entered";
            if (f.MarkedValue != null) return "Marked";
            return "Unclassified";
        }

        // Shared projection of a stored file into the API-facing SubmissionFile,
        // reused by submission review, prior-file lookup, and exhibit search so the
        // classification/status shape stays consistent in one place.
        public static SubmissionFile ToSubmissionFile(this StoredFiles f) => new()
        {
            Id = f.Id,
            OriginalFileName = f.OriginalFileName,
            StoredFileName = f.StoredFileName,
            ContentType = f.ContentType,
            FileSize = f.FileSize,
            StorageProvider = f.StorageProvider,
            Url = "",
            Status = f.DeriveStatus(),
            MarkedValue = f.MarkedValue,
            MarkedAt = f.MarkedAt,
            EnteredValue = f.EnteredValue,
            EnteredAt = f.EnteredAt,
            Descriptions = f.Descriptions
                .OrderBy(d => d.CreatedAtUTC)
                .Select(d => new ExhibitDescriptionEntryModel
                {
                    Id = d.Id,
                    DescriptionText = d.DescriptionText,
                    CreatedBy = d.CreatedBy,
                    CreatedAtUTC = d.CreatedAtUTC,
                })
                .ToList(),
            EvidenceSourceType = f.EvidenceSourceType,
            DeletedAt = f.DeletedAtUTC,
        };
    }
}
