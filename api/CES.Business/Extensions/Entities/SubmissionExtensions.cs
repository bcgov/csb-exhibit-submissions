using CES.Business.Models;
using CES.Entities;

namespace CES.Business.Extensions.Entities
{
    public static class SubmissionExtensions
    {
        public static Submission ToEntity(this EvidenceSubmissionModel model)
        {
            var entity = new Submission
            {
                AccusedDOB = model.AccusedDOB,
                AccusedName = model.AccusedName,
                AppearanceDateTime = model.AppearanceDateTime,
                AppearanceID = model.AppearanceID,
                CourtListType = model.CourtListType,
                FileNumberText = model.FileNumberText,
                LocationId = model.LocationId,
                LocationNameText = model.LocationNameText,
                RoomCode = model.RoomCode,
                RoomText = model.RoomText,
                OfficerNumber = model.OfficerNumber
            };

            return entity;
        }

        public static SubmissionReviewModel ToReviewModel(this Submission entity)
        {
            return new SubmissionReviewModel
            {
                SubmissionDate = entity.UploadDate,
                CourtDateTime = entity.AppearanceDateTime ?? "",
                AccusedName = entity.AccusedName ?? "",
                Id = entity.Id,
                Location = entity.LocationNameText ?? "",
                Room = entity.RoomText ?? "",
                FileNumber = entity.FileNumberText,

                Files = entity.Files.Select(f => new SubmissionFile
                            {
                                ContentType = f.ContentType,
                                FileSize = f.FileSize,
                                Id = f.Id,
                                OriginalFileName = f.OriginalFileName,
                                StorageProvider = f.StorageProvider,
                                StoredFileName = f.StoredFileName,
                                Url = "",
                                
                            }).ToList()
            };
        }
    }
    
}