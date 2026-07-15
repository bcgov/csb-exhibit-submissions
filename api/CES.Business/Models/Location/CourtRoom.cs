using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CES.Business.Models.Location
{
    public class CourtRoom
    {
        public required string Name { get; set; }
        public required string Code { get; set; }
        public required string LocationId { get; set; }
        public required string Type { get; set; }
    }
}
