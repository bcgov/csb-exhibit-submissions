using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CES.Business.Models.Location
{
    public class CourtList
    {
        public string AppearanceId { get; set; } = string.Empty;
        public string? AppearanceDateTime { get; set; }
        public string? AppearanceSequenceNumber { get; set; } = string.Empty;
        public string? AppearanceReasonCode { get; set; } = string.Empty;
        public string? CourtListType { get; set; } = string.Empty;
        public string FileNumberText { get; set; } = string.Empty;
        public string LocationId { get; set; } = string.Empty;
        public string? LocationNameText { get; set; } = string.Empty;
        public string RoomCode { get; set; } = string.Empty;
        public string? RoomText { get; set; } = string.Empty;
        public string? AccusedName { get; set; } = string.Empty;
        public string? AccusedDOB { get; set; } = string.Empty;
        public ICollection<AppearanceDetails> AppearanceDetails { get; set; } = [];

    }

    public class AppearanceDetails
    {
        public string CountPrintSequenceNumber { get; set; } = string.Empty;
        public string StatuteDescription { get; set; } = string.Empty;
        public string AppearanceReasonCode { get; set; } = string.Empty;
    }
}
