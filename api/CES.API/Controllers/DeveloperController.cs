using CES.Business.Interfaces;
using CES.Business.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CES.API.Controllers
{
    public class DeveloperController : Controller
    {
        private IDeveloperService _developerService { get; set; }

        public DeveloperController(IDeveloperService developerService)
        {
            _developerService = developerService;
        }

        [HttpGet]
        [Route("api/dev/health")]
        public IActionResult HealthCheck()
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = _developerService.HealthCheck();
            if (result)
            {
                return Ok("API is up");
            }
            return BadRequest("Something failed");
        }
    }
}