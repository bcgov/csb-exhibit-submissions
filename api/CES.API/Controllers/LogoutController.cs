using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;

namespace CES.API.Controllers
{
    public class LogoutController : Controller
    {
       public LogoutController() { }

        [HttpPost]
        [Route("api/logout")]
        [Authorize]
        public IActionResult LogoutUser()
        {
            return Ok();
        }
    }
}
