using CES.Business.Interfaces;
using CES.Business.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CES.API.Controllers
{
    public class FilesController : Controller
    {
        private readonly IFileStorage _fileStorage;
        private readonly IFileService _fileService;

        public FilesController(IFileStorage fileStorage, IFileService fileService)
        {
            _fileStorage = fileStorage;
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
        // [Authorize(Roles = "Admin")]
        public async Task<IActionResult> View(Guid fileId)
        {
            var file = await _fileService.RetrieveFileMetaData(fileId);

            if (file == null)
                return NotFound();

            if (file.IsDeleted)
                return NotFound();

            var stream = await _fileStorage.GetAsync(file);

            return new FileStreamResult(stream, file.ContentType) { EnableRangeProcessing = true };
        }

        [HttpGet]
        [Route("api/files/{fileId}/download")]
        // [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Download(Guid fileId)
        {
            var file = await _fileService.RetrieveFileMetaData(fileId);

            if (file == null)
                return NotFound();

            if (file.IsDeleted)
                return NotFound();

            var stream = await _fileStorage.GetAsync(file);

            return File(stream, file.ContentType, file.OriginalFileName);
        }
    }
}
