using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CES.Business.Models.Location;
using JCCommon.Clients.LocationServices;

namespace CES.Business.Extensions
{
    public static class CodeValueExtensions
    {
        
        public static Location ConvertToLocationModel(this CodeValue value)
        {
            return new Location{Code = value.Code,
            Name = value.LongDesc,
            ShortName = value.ShortDesc,
            LocationId = value.ShortDesc};
        }
        public static CourtRoom ConvertToCourtRoomModel(this CodeValue value)
        {
            return new CourtRoom
            {
                Code = value.Code,
                LocationId = value.Flex,
                Name = value.LongDesc,
                Type = value.ShortDesc
            };
        }
    }
}