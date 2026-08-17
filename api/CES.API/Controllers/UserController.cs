using CES.API.Authentication;
using CES.Business.Interfaces;
using CES.Business.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CES.API.Controllers
{
    /// <summary>
    /// The signed-in user's own CES-local record. Every action resolves the caller from the
    /// token's claims — never from a route or body parameter — so one user can neither read nor
    /// write another's profile.
    /// </summary>
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// The caller's profile, including the officer number the SPA prefills submissions with.
        /// </summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetProfile()
        {
            var profile = await _userService.GetProfileAsync(User.GetSubject(), User.GetEmail())
                ?? throw new KeyNotFoundException("No user record was found for the signed-in user.");

            return Ok(profile);
        }

        /// <summary>
        /// Stores the officer number collected by the Court Search prompt. Kept separate from any
        /// general profile update: it is the only field on the row a user may write, since name and
        /// email are owned by IDIR.
        /// </summary>
        [HttpPut("me/officer-number")]
        public async Task<IActionResult> UpdateOfficerNumber([FromBody] OfficerNumberUpdateModel model)
        {
            var userId = await User.ResolveUserIdAsync(_userService)
                ?? throw new KeyNotFoundException("No user record was found for the signed-in user.");

            return Ok(await _userService.SetOfficerNumberAsync(userId, model?.OfficerNumber));
        }
    }
}
