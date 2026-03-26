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
            return new Location();
        }
        public static CourtRoom ConvertToCourtRoomModel(this CodeValue value)
        {
            return new CourtRoom();
        }
    }
}