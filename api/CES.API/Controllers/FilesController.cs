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

        // Description entries (CES-42). Append-only — there is deliberately no update
        // or delete route, and no GET: the entries ride along on every SubmissionFile.
        [HttpPost]
        [Route("api/files/{fileId:guid}/descriptions")]
        [Authorize(Roles = "User,Admin")]
        public async Task<IActionResult> AddDescription(Guid fileId, [FromBody] AddExhibitDescriptionModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var isAdmin = User.IsInRole("Admin");
            var createdBy = User.FindFirstValue(ClaimTypes.UserData) ?? (isAdmin ? "Admin" : "Officer");
            var result = await _fileService.AddExhibitDescriptionAsync(fileId, model.DescriptionText, createdBy, isAdmin);
            return Ok(result);
        }

        [HttpPatch]
        [Route("api/files/{fileId:guid}/evidence-source")]
        [Authorize(Roles = "User,Admin")]
        public async Task<IActionResult> UpdateEvidenceSource(Guid fileId, [FromBody] ExhibitEvidenceSourceModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var isAdmin = User.IsInRole("Admin");
            var changedBy = User.FindFirstValue(ClaimTypes.UserData) ?? (isAdmin ? "Admin" : "Officer");
            var result = await _fileService.UpdateExhibitEvidenceSourceAsync(fileId, model.EvidenceSourceType, changedBy, isAdmin);
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

        // Registry-only notes (CES-38 extension). Admin (JJ/registry) only — these are
        // protected and never exposed to officers.
        [HttpGet]
        [Route("api/files/{fileId:guid}/notes")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetNotes(Guid fileId)
        {
            var result = await _fileService.GetExhibitNotesAsync(fileId);
            return Ok(result);
        }

        [HttpPost]
        [Route("api/files/{fileId:guid}/notes")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddNote(Guid fileId, [FromBody] AddExhibitNoteModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var createdBy = User.FindFirstValue(ClaimTypes.UserData) ?? "Admin";
            var result = await _fileService.AddExhibitNoteAsync(fileId, model.NoteText, createdBy);
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
