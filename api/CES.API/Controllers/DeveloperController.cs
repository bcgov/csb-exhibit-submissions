using CES.API.FileStorage.Smb;
using CES.Business.Constants;
using CES.Business.Interfaces;
using CES.Business.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CES.API.Controllers
{
    public class DeveloperController : Controller
    {
        private IDeveloperService _developerService { get; set; }
        private ISmbDiagnosticsService _smbDiagnosticsService { get; set; }

        public DeveloperController(
            IDeveloperService developerService,
            ISmbDiagnosticsService smbDiagnosticsService)
        {
            _developerService = developerService;
            _smbDiagnosticsService = smbDiagnosticsService;
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

        // Stage 1 SMB diagnostic (spec/smb-file-storage.md). Connects, logs in, lists the
        // server's shares, tree-connects, lists the base path and optionally reads a
        // probe file — reporting each step's own outcome and raw NTStatus.
        //
        // Doubly gated: admin role, plus Development or FileStorage:Smb:DiagnosticsEnabled.
        // It returns 404 rather than 403 when disabled so it does not advertise itself.
        //
        // Always answers 200 when it runs, even when a step fails: "how far did we get"
        // is what the endpoint is for, so a failure is data, not an error.
        [HttpGet]
        [Route("api/dev/smb/health")]
        [Authorize(Roles = RoleConstants.Admin)]
        public async Task<IActionResult> SmbHealthCheck(CancellationToken cancellationToken)
        {
            if (!_smbDiagnosticsService.IsEnabled)
                return NotFound();

            var result = await _smbDiagnosticsService.CheckHealthAsync(cancellationToken);
            return Ok(result);
        }
    }
}
