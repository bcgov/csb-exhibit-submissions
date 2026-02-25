using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CES.Entities.Enums
{
    public enum ApplicationRoles
    {
        [Description("SuperAdmin")]
        SuperAdmin=0,

        [Description("Admin")]
        Admin=1
    }
}
