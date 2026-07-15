using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CES.Business.Models
{
    public class LoggedInUserModel
    {
        public int UserId {  get; set; }
        public required string Email { get; set; }
        public required string LastActiveRole { get; set; }
    }
}
