using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CES.Entities.Infrastructure
{
    public static class SystemDate
    {
        public static Func<DateTime> Now = () => DateTime.Now.ToLocalTime();
        public static Func<DateTime> UtcNow = () => DateTime.Now.ToUniversalTime();
    }
}
