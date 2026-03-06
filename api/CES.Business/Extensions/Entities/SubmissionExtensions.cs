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
                DisputantName = model.DisputantName,
                Location = model.Location,
                OfficerNumber = model.OfficerNumber,
                Room = model.Room,
                TicketNumber = model.TicketNumber
            };

            return entity;
        }

        public static SubmissionReviewModel ToReviewModel(this Submission entity)
        {
            return new SubmissionReviewModel
            {
                Date = entity.UploadDate,
                DisputantName = entity.DisputantName,
                Id = entity.Id,
                Location = entity.Location,
                OfficerNumber = entity.OfficerNumber,
                Room = entity.Room,
                TicketNumber = entity.TicketNumber,

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