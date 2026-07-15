using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CES.Business.Models
{
    public class TemplatedEmailModel
    {
        public required string ToEmailAddress { get; set; }
        public required string ToName { get; set; }
        public required string Subject { get; set; }
        public required string Body { get; set; }
    }
}
