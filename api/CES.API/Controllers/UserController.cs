using CES.Business.Extensions.Entities;
using CES.Business.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CES.API.Controllers
{
    public class UserController : Controller
    {
        public UserController() 
        {

        }

        [HttpPost]
        [Route("api/users/createUser")]
        [Authorize]
        public IActionResult CreateUsers()
        {
            var user = User.ToLoggedInUserModel();
            return Ok(new {returnVal=1});
        }
    }
}
