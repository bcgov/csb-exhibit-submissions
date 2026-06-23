using CES.API.Models;
using CES.Business.Interfaces;
using CES.Business.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CES.API.Controllers
{
    public class SubmissionController : Controller
    {
        private readonly ISubmissionService _submissionService;

        public SubmissionController(ISubmissionService submissionService)
        {
            _submissionService = submissionService;
        }

        [HttpPost]
        [Route("api/submissions/submit")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> SubmitEvidence([FromForm] SubmissionModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (model.Tickets == null || model.Tickets.Count == 0)
                return BadRequest("At least one ticket is required.");

            model.fileUploads = model.Files.Select(f => new FileUpload
            {
                Location = model.LocationId,
                Date = model.ShortDate,
                Room = model.RoomCode,
                FileName = f.FileName,
                ContentType = f.ContentType,
                Length = f.Length,
                Content = f.OpenReadStream()
            }).ToList();

            if (model.fileUploads.Count == 0)
                return BadRequest("No files uploaded.");

            var result = await _submissionService.SubmitEvidence(model);
            return result ? Ok("Submission accepted") : BadRequest("Something failed");
        }

        [HttpGet]
        [Route("api/submissions/retrieve")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RetrieveSubmission([FromQuery] int fileId)
        {
            var model = await _submissionService.RetrieveSubmission(fileId);
            if (model == null)
                return NotFound();

            return Ok(model);
        }

        [HttpGet]
        [Route("api/submissions/listing")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RetrieveSubmissionListing()
        {
            var model = await _submissionService.RetrieveSubmissionListing();
            return Ok(model);
        }

        [HttpGet]
        [Route("api/submissions/by-file-number")]
        [Authorize(Roles = "User,Admin")]
        public async Task<IActionResult> GetSubmissionsByFileNumber([FromQuery] string fileNumberText)
        {
            if (string.IsNullOrWhiteSpace(fileNumberText))
                return BadRequest("fileNumberText is required.");

            var result = await _submissionService.GetSubmissionsByFileNumberAsync(fileNumberText);
            return Ok(result);
        }

        [HttpPost]
        [Route("api/submissions/accept")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AcceptSubmissions([FromBody] EvidenceAcceptanceModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (model.acceptedFiles.Count == 0 || model.FileId == 0)
                return BadRequest("No files accepted");

            var result = await _submissionService.AcceptSubmissions(model);
            return result ? Ok("Submission accepted") : BadRequest("Something failed");
        }

        [HttpPost]
        [Route("api/submissions/reject")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RejectSubmissions([FromBody] EvidenceAcceptanceModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _submissionService.RejectSubmissions(model);
            return result ? Ok("Submission accepted") : BadRequest("Something failed");
        }

        [HttpDelete]
        [Route("api/submissions/files/{fileId:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveFile(Guid fileId)
        {
            var result = await _submissionService.RemoveFileAsync(fileId);
            return result ? Ok() : NotFound();
        }
    }
}
