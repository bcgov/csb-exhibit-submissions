using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CES.Business.Models.Location
{
    public class CourtList
    {
        public string AppearanceID { get; set; } = string.Empty;
        public DateTime? AppearanceDateTime { get; set; }
        public string CourtListType { get; set; } = string.Empty;
        public string FileNumberText { get; set; } = string.Empty;
        public string LocationId { get; set; } = string.Empty;
        public string LocationNameText { get; set; } = string.Empty;
        public string RoomCode { get; set; } = string.Empty;
        public string RoomText { get; set; } = string.Empty;
        public string AccusedName { get; set; } = string.Empty;
        public string AccusedDOB { get; set; } = string.Empty;

    }
}
