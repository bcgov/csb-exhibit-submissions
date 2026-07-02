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
                ShortDate = model.ShortDate,
                AppearanceDateTime = model.AppearanceDateTime,
                LocationId = model.LocationId,
                LocationNameText = model.LocationNameText,
                RoomCode = model.RoomCode,
                RoomText = model.RoomText,
                OfficerNumber = model.OfficerNumber,
                Tickets = model.Tickets.Select(t => new SubmissionTicket
                {
                    AppearanceId = t.AppearanceId,
                    AppearanceDateTime = t.AppearanceDateTime,
                    AppearanceSequenceNumber = t.AppearanceSequenceNumber,
                    AppearanceReasonCode = t.AppearanceReasonCode,
                    CourtListType = t.CourtListType,
                    FileNumberText = t.FileNumberText,
                    AccusedName = t.AccusedName,
                    AccusedDOB = t.AccusedDOB
                }).ToList()
            };

            return entity;
        }

        public static SubmissionReviewModel ToReviewModel(this Submission entity)
        {
            var firstTicket = entity.Tickets.FirstOrDefault();

            return new SubmissionReviewModel
            {
                Id = entity.Id,
                SubmissionDate = entity.UploadDate,
                CourtDateTime = firstTicket?.AppearanceDateTime ?? "",
                Location = entity.LocationNameText ?? "",
                Room = entity.RoomText ?? "",
                Status = entity.Status.ToString(),
                StatusChangedDate = entity.StatusChangedDateUTC,
                ExhibitCount = entity.Files.Count(f => !f.IsDeleted),
                Tickets = entity.Tickets.Select(t => new SubmissionTicketModel
                {
                    AppearanceId = t.AppearanceId,
                    AppearanceDateTime = t.AppearanceDateTime,
                    AppearanceSequenceNumber = t.AppearanceSequenceNumber,
                    AppearanceReasonCode = t.AppearanceReasonCode,
                    CourtListType = t.CourtListType,
                    FileNumberText = t.FileNumberText,
                    AccusedName = t.AccusedName,
                    AccusedDOB = t.AccusedDOB
                }).ToList(),
                Files = entity.Files.Select(f => new SubmissionFile
                {
                    ContentType = f.ContentType,
                    FileSize = f.FileSize,
                    Id = f.Id,
                    OriginalFileName = f.OriginalFileName,
                    StorageProvider = f.StorageProvider,
                    StoredFileName = f.StoredFileName,
                    Url = "",
                    Status = f.DeriveStatus(),
                    MarkedValue = f.MarkedValue,
                    MarkedAt = f.MarkedAt,
                    EnteredValue = f.EnteredValue,
                    EnteredAt = f.EnteredAt,
                    Description = f.Description,
                    DeletedAt = f.DeletedAtUTC,
                }).ToList()
            };
        }
    }
}
