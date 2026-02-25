using CES.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CES.Business.Models
{
    public class CESLoginReturnModel
    {
        public int UserId { get; set; } = 0;
        public string? AuthToken { get; set; }
        public string? UserName { get; set; }
        public string? TokenExpiryUTC { get; set; }
        public bool ChangePassword { get; set; } = false;
        public ApplicationRoles? LastActiveRole { get; set; }

    }
}
