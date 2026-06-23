using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Entities.Interfaces;
using CES.Business.Infrastructure;
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
        [Authorize(Roles = "User")]
        public async Task<IActionResult> MarkExhibit(Guid fileId, [FromBody] ExhibitMarkModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var changedBy = User.FindFirstValue(ClaimTypes.UserData) ?? "Officer";
            var result = await _fileService.MarkExhibitAsync(fileId, model.MarkedValue, changedBy);
            return Ok(result);
        }

        [HttpPost]
        [Route("api/files/{fileId:guid}/enter")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> EnterExhibit(Guid fileId, [FromBody] ExhibitEnterModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var changedBy = User.FindFirstValue(ClaimTypes.UserData) ?? "Officer";
            var result = await _fileService.EnterExhibitAsync(fileId, model.EnteredValue, changedBy);
            return Ok(result);
        }

        [HttpPatch]
        [Route("api/files/{fileId:guid}/description")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> UpdateDescription(Guid fileId, [FromBody] ExhibitDescriptionModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var changedBy = User.FindFirstValue(ClaimTypes.UserData) ?? "Officer";
            var result = await _fileService.UpdateExhibitDescriptionAsync(fileId, model.Description, changedBy);
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

            var stream = await _fileStorage.GetAsync(file);

            // return File(stream, file.ContentType, enableRangeProcessing: true);
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

            var stream = await _fileStorage.GetAsync(file);

            return File(stream, file.ContentType, file.OriginalFileName);
        }


/*
    WIP: To secure the file view and downloading so that users have to be authorized.
    current /view and /download links are not secure if someone knows/guesses the file GUID
*/
        // [Authorize]
        // [HttpGet("{id}/stream-url")]
        // public async Task<IActionResult> GetStreamUrl(Guid fileId)
        // {
        //     var file = await _fileService.RetrieveFileMetaData(fileId);
        //     if (file == null)
        //         return NotFound();

        //     // expires in 2 minutes
        //     var expires = DateTime.UtcNow.AddMinutes(2);

        //     var token = await CES.Business.Services.CryptographyService.GenerateVideoViewToken(file.Id, expires);

        //     var url = $"{Request.Scheme}://{Request.Host}/api/files/stream/{file.Id}?token={token}";

        //     return Ok(new { url });
        // }

        // [AllowAnonymous]
        // [HttpGet("stream/{id}")]
        // public async Task<IActionResult> Stream(Guid fileId, string token)
        // {
        //     var isTokenValid = await CES.Business.Services.CryptographyService.ValidateVideoToken(fileId, token);
        //     if (!isTokenValid)
        //         return Unauthorized();

        //     var file = await _fileService.RetrieveFileMetaData(fileId);
        //     if (file == null)
        //         return NotFound();

        //     var stream = await _fileStorage.GetAsync(file);

        //     // return File(stream, file.ContentType, enableRangeProcessing: true);
        //     return new FileStreamResult(stream, file.ContentType) { EnableRangeProcessing = true };
        // }
    }
}