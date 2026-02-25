using CES.Business.Interfaces;
using CES.Business.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CES.API.Controllers
{
    public class LoginController : Controller
    {
        public ILoginService _loginService {  get; set; }
        public LoginController(ILoginService loginService) 
        {
            _loginService = loginService;
        }

        [HttpPost]
        [Route("api/login")]
        public IActionResult LoginUser([FromBody] CESLoginModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = _loginService.LoginUser(model);
            if (result == null)
            {
                return BadRequest("Invalid Username and password");
            }
            return Ok(true);
        }

        
    }
}
