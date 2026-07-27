using CES.API.Authentication;
using CES.Business.Constants;
using CES.Business.Interfaces;
using CES.Business.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CES.API.Controllers
{
    public class LoginController : Controller
    {
        public ITokenService _tokenService {get;set;}
        public LoginController(ITokenService tokenService) 
        {
            _tokenService = tokenService;
        }

        [HttpPost]
        [Route("api/auth/login")]
        public IActionResult LoginUser([FromBody] CESLoginModel model)
        {
            var mockUsers = new Dictionary<string, (string Password, string Role)>
            {
                { "admin@gov.bc.ca", ("pass123", RoleConstants.Admin) },
                { "officer@gov.bc.ca", ("pass123", RoleConstants.User) },
                { "clerk@gov.bc.ca", ("pass123", RoleConstants.Clerk) }
            };
            if (mockUsers.TryGetValue(model.Username.ToLower(), out var userRecord) && 
                userRecord.Password == model.Password)
            {
                // Generate the token using the service we built earlier
                var token = _tokenService.GenerateToken(model.Username, userRecord.Role);
                return Ok(new { token });
            }

            return Unauthorized();
        }

        
    }
}
