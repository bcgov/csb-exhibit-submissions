using CES.Entities.Enums;
using CES.Entities.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CES.Entities
{
    public class EmailMessage : BaseEntity
    {
        public EmailMessageStatus Status { get; set; }

        public string FromName { get; set; }
        public string FromEmailAddress { get; set; }

        public string ToName { get; set; }
        public string ToEmailAddress { get; set; }

        public string Subject { get; set; }
        public string Body { get; set; }

        public DateTime? SentDateTimeUTC { get; set; }

        // Capture error if it fails to send
        public string ErrorMessage { get; set; } = string.Empty;
        public int ErrorsEncountered { get; set; } = 0;
    }
}
