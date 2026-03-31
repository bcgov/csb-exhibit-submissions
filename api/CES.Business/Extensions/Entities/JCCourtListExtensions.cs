// JCCommon.Clients.FileServices.CourtList


using CES.Business.Models;
using CES.Business.Models.Location;
using CES.Entities;
using CES.Entities.Infrastructure;
using JCCommon.Clients.FileServices;

namespace CES.Business.Extensions.Entities
{
    public static class JCCourtListExtensions
    {
        public static Models.Location.CourtList ToLocalEntity(this JCCommon.Clients.FileServices.ClCriminalCourtList model)//, string locationId, string locationName, string courtRoomCode)
        {
            var entity = new Models.Location.CourtList
            {
                AppearanceID = model.CriminalAppearanceID,
                AppearanceDateTime = model.AppearanceTime,
                AppearanceSequenceNumber = model.AppearanceSequenceNumber,
                AppearanceReasonCode = model.AppearanceCount.Count() > 0 ? model.AppearanceCount.ToList()[0].AppearanceReasonCode : "",
                CourtListType = model.CourtListTypeCd,
                FileNumberText = model.FileNumberText,
                AccusedName = model.AccusedFullName,
                AccusedDOB = model.AccusedBirthDate,
                AppearanceDetails = model.AppearanceCount.Count() > 0 ? model.AppearanceCount.Select(a => a.ToListingEntity()).ToList() : []
            };

            return entity;
        }

        public static AppearanceDetails ToListingEntity(this JCCommon.Clients.FileServices.ClAppearanceCount model)
        {
            var details = new AppearanceDetails{
                AppearanceReasonCode = model.AppearanceReasonCode,
                CountPrintSequenceNumber = model.CountPrintSequenceNumber,
                StatuteDescription = model.ChargeStatuteDescription
            };


            return details;
        }
        // public static CourtList ToLocalEntity(this JCCommon.Clients.FileServices.ClCivilCourtList model)//, string locationId, string locationName, string courtRoomCode)
        // {
        //     var entity = new CourtList
        //     {
        //         AppearanceID = model.AppearanceId,
        //         AppearanceDateTime = model.AppearanceDate != null ? DateTime.Parse(model.AppearanceTime) : SystemDate.UtcNow(),
        //         CourtListType = model.CourtListTypeCd,
        //         FileNumberText = model.FileNumberText,
        //         // LocationId = locationId,
        //         // LocationNameText = locationName,
        //         // RoomCode = courtRoomCode,
        //         // RoomText = courtRoomCode,
        //         AccusedName = model.AccusedFullName,
        //         AccusedDOB = model.AccusedBirthDate
        //     };

        //     return entity;
        // }
    }
}