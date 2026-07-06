using CES.Business.Interfaces;
using CES.Business.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CES.API.Controllers
{
    public class FilesController : Controller
    {
        private readonly IFileService _fileService;

        public FilesController(IFileService fileService)
        {
            _fileService = fileService;
        }

        [HttpPost]
        [Route("api/files/{fileId:guid}/mark")]
        [Authorize(Roles = "User,Admin")]
        public async Task<IActionResult> MarkExhibit(Guid fileId, [FromBody] ExhibitMarkModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var isAdmin = User.IsInRole("Admin");
            var changedBy = User.FindFirstValue(ClaimTypes.UserData) ?? (isAdmin ? "Admin" : "Officer");
            var result = await _fileService.MarkExhibitAsync(fileId, model.MarkedValue, changedBy, isAdmin);
            return Ok(result);
        }

        [HttpPost]
        [Route("api/files/{fileId:guid}/enter")]
        [Authorize(Roles = "User,Admin")]
        public async Task<IActionResult> EnterExhibit(Guid fileId, [FromBody] ExhibitEnterModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var isAdmin = User.IsInRole("Admin");
            var changedBy = User.FindFirstValue(ClaimTypes.UserData) ?? (isAdmin ? "Admin" : "Officer");
            var result = await _fileService.EnterExhibitAsync(fileId, model.EnteredValue, changedBy, isAdmin);
            return Ok(result);
        }

        [HttpPatch]
        [Route("api/files/{fileId:guid}/description")]
        [Authorize(Roles = "User,Admin")]
        public async Task<IActionResult> UpdateDescription(Guid fileId, [FromBody] ExhibitDescriptionModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var isAdmin = User.IsInRole("Admin");
            var changedBy = User.FindFirstValue(ClaimTypes.UserData) ?? (isAdmin ? "Admin" : "Officer");
            var result = await _fileService.UpdateExhibitDescriptionAsync(fileId, model.Description, changedBy, isAdmin);
            return Ok(result);
        }

        [HttpGet]
        [Route("api/files/{fileId:guid}/history")]
        [Authorize(Roles = "User,Admin")]
        public async Task<IActionResult> GetHistory(Guid fileId)
        {
            var result = await _fileService.GetExhibitHistoryAsync(fileId);
            return Ok(result);
        }

        [HttpGet]
        [Route("api/files/{fileId}/view")]
        // NOTE: intentionally left open — the frontend loads previews via a raw
        // <video>/<img>/<iframe> src that cannot carry the JWT Bearer token. Re-enable
        // [Authorize(Roles = "User,Admin")] once authenticated blob or signed-URL
        // streaming is wired up on the client (CES-39, deferred).
        // [Authorize(Roles = "User,Admin")]
        public async Task<IActionResult> View(Guid fileId)
        {
            var (stream, _, contentType, error) = await _fileService.GetExhibitContentAsync(fileId);

            if (stream == null)
                return NotFound(error);

            return new FileStreamResult(stream, contentType ?? "application/octet-stream") { EnableRangeProcessing = true };
        }

        [HttpGet]
        [Route("api/files/{fileId}/download")]
        // NOTE: intentionally left open — see the View endpoint above. The admin
        // download uses a bare fetch() without the Bearer token. Re-enable
        // [Authorize(Roles = "User,Admin")] alongside the client-side auth fix.
        // [Authorize(Roles = "User,Admin")]
        public async Task<IActionResult> Download(Guid fileId)
        {
            var (stream, fileName, contentType, error) = await _fileService.GetExhibitContentAsync(fileId);

            if (stream == null)
                return NotFound(error);

            return File(stream, contentType ?? "application/octet-stream", fileName);
        }
    }
}
