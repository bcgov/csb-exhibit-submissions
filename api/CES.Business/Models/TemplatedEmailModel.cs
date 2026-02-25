using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CES.Business.Models
{
    public class TemplatedEmailModel
    {
        public string ToEmailAddress { get; set; }
        public string ToName { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
    }
}
