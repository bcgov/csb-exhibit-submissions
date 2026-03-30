// JCCommon.Clients.FileServices.CourtList


using CES.Business.Models;
using CES.Business.Models.Location;
using CES.Entities;
using CES.Entities.Infrastructure;

namespace CES.Business.Extensions.Entities
{
    public static class JCCourtListExtensions
    {
        public static CourtList ToLocalEntity(this JCCommon.Clients.FileServices.ClCriminalCourtList model)//, string locationId, string locationName, string courtRoomCode)
        {
            var entity = new CourtList
            {
                AppearanceID = model.CriminalAppearanceID,
                AppearanceDateTime = model.AppearanceTime != null ? DateTime.Parse(model.AppearanceTime) : SystemDate.UtcNow(),
                CourtListType = model.CourtListTypeCd,
                FileNumberText = model.FileNumberText,
                // LocationId = locationId,
                // LocationNameText = locationName,
                // RoomCode = courtRoomCode,
                // RoomText = courtRoomCode,
                AccusedName = model.AccusedFullName,
                AccusedDOB = model.AccusedBirthDate
            };

            return entity;
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