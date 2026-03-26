using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CES.Business.Models.Location
{
    public class CourtList
    {
        public string AppearanceID { get; set; } = string.Empty;
        public DateTime? AppearanceTime { get; set; }
        public string CourtListType { get; set; } = string.Empty;
        public string AccusedName { get; set; } = string.Empty;

    }
}
