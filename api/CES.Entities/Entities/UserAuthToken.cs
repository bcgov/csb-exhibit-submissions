using CES.Entities.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CES.Entities
{
    public class UserAuthToken:BaseEntity
    {
        public ApplicationUser ApplicationUser { get; set; }
        public string AuthToken { get; set; }
        public DateTime TokenExpiryDateUTC { get; set; }
        public bool? IsRevoked { get; set; }
        public DateTime? RevokedDateUTC { get; set; }
    }
}
