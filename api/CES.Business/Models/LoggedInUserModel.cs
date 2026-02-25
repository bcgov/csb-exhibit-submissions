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
        public string Email { get; set; }   
        public string LastActiveRole { get; set; }
    }
}
