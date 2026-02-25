using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CES.Entities.Infrastructure
{
    public class BaseEntity
    {
        public int Id { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedDateUTC { get; set; }

        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedDateUTC { get; set; }

        public bool IsDeleted { get; set; }

        public BaseEntity()
        {
            CreatedBy = "0";
            CreatedDateUTC = SystemDate.Now();
            IsDeleted = false;
        }

        public void SetUpdateBy(string updator = "System")
        {
            UpdatedBy = updator;
            UpdatedDateUTC = SystemDate.Now();
        }
    }
}
