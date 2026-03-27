// JCCommon.Clients.FileServices.CourtList


using CES.Business.Models;
using CES.Business.Models.Location;
using CES.Entities;
using CES.Entities.Infrastructure;

namespace CES.Business.Extensions.Entities
{
    public static class JCCourtListExtensions
    {
        public static CourtList ToLocalEntity(this JCCommon.Clients.FileServices.ClCriminalCourtList model)
        {
            var entity = new CourtList
            {
                AccusedName = model.AccusedFullName,
                AppearanceID = model.CriminalAppearanceID,
                AppearanceTime = model.AppearanceTime != null ? DateTime.Parse(model.AppearanceTime) : SystemDate.UtcNow(),
                CourtListType = model.CourtListTypeCd
            };

            return entity;
        }
    }
}