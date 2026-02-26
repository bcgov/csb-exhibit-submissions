using CES.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CES.Business.Models
{
    public class UserModel
    {
        public int Id { get; set; } = 0;
        public string FirstName { get; set; }=string.Empty;
        public string LastName { get; set; }= string.Empty;
        public string Email { get; set; } = string.Empty;

    }
}
