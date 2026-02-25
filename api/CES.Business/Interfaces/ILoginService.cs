using CES.Business.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CES.Business.Interfaces
{
    public interface ILoginService
    {
        public CESLoginReturnModel LoginUser(CESLoginModel model);
    }
}
