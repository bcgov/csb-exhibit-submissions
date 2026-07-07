using CES.Business.Models;
using CES.Entities;
using CES.Entities.Enums;
using CES.Entities.Infrastructure;

namespace CES.Business.Extensions.Entities
{
    public static class SubmissionExtensions
    {
        // Derives submission status from its files (CES-39). With whole-submission
        // Accept retired, a submission reads as Accepted once every non-deleted file
        // is accepted, and flips back to Pending if a new un-accepted file is added
        // to it (same-session uploads reuse one submissionId). Rejected is terminal
        // and never re-derived. Requires Files to be loaded.
        public static void RecalculateStatus(this Submission submission)
        {
            if (submission.Status == SubmissionStatus.Rejected)
                return;

            var retained = submission.Files.Where(f => !f.IsDeleted).ToList();
            var newStatus = retained.Count > 0 && retained.All(f => f.IsAccepted)
                ? SubmissionStatus.Accepted
                : SubmissionStatus.Pending;

            if (newStatus != submission.Status)
            {
                submission.Status = newStatus;
                submission.StatusChangedDateUTC = SystemDate.UtcNow();
            }
        }

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
                Files = entity.Files.Select(f => f.ToSubmissionFile()).ToList()
            };
        }
    }
}
