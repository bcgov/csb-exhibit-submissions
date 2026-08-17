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
        private readonly IUserService _userService;

        public LoginController(ITokenService tokenService, IUserService userService)
        {
            _tokenService = tokenService;
            _userService = userService;
        }

        [HttpPost]
        [Route("api/auth/login")]
        public async Task<IActionResult> LoginUser([FromBody] CESLoginModel model)
        {
            if (!DevBypassUsers.All.TryGetValue(model.Username.ToLower(), out var userRecord) ||
                userRecord.Password != model.Password)
            {
                return Unauthorized();
            }

            // Provision the local row before minting the token so this session's audit
            // writes resolve to a real ApplicationUser.Id, exactly as a Keycloak login does.
            await _userService.UpsertMockUserAsync(
                userRecord.Email, userRecord.FirstName, userRecord.LastName);

            // Generate the token using the service we built earlier
            var token = _tokenService.GenerateToken(model.Username, userRecord.Role);
            return Ok(new { token });
        }
    }
}
