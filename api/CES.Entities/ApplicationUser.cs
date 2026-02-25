using CES.Entities.Enums;
using CES.Entities.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CES.Entities
{
    public class ApplicationUser:BaseEntity
    {
        public string FirstName { get;set; }
        public string LastName { get;set; }
        public string Email {  get;set; } 
        public string Password {  get;set; }
        public bool IsActive {  get;set; }

        public string GetFullName()
        {
            return $"{FirstName} {LastName}";
        }
    }
}
