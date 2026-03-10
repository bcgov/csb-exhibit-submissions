using CES.API.Models;
using CES.Business.Interfaces;
using CES.Business.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CES.API.Controllers
{
    public class SubmissionController : Controller
    {
        private ISubmissionService _submissionService {get;set;}

        public SubmissionController(ISubmissionService submissionService)
        {
            _submissionService = submissionService;
        }

        [HttpPost]
        [Route("api/submissions/submit")]
        public async Task<IActionResult> SubmitEvidence([FromForm] SubmissionModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            model.fileUploads = model.Files.Select(f => new FileUpload
                                                            {
                                                                FileName = f.FileName,
                                                                ContentType = f.ContentType,
                                                                Length = f.Length,
                                                                Content = f.OpenReadStream()
                                                            }).ToList();

            if(model.fileUploads.Count == 0)
                return BadRequest("No files uploaded");

            var result = await _submissionService.SubmitEvidence(model);


            return result ? Ok("Submission accepted") : BadRequest("Something failed");
        }

        [HttpGet]
        [Route("api/submissions/retrieve")]
        public async Task<IActionResult> RetrieveSubmission([FromQuery]int fileId)
        {
            var model = await _submissionService.RetrieveSubmission(fileId);
            if (model == null)
            {
                return NotFound();
            }
            return Ok(model);
        }

        [HttpGet]
        [Route("api/submissions/listing")]
        public async Task<IActionResult> RetrieveSubmissionListing()
        {
            
            var model = await _submissionService.RetrieveSubmissionListing();
            if (model == null)
            {
                return NotFound();
            }
            return Ok(model);
        }
        
        [HttpPost]
        [Route("api/submissions/accept")]
        public async Task<IActionResult> AcceptSubmissions([FromBody] EvidenceAcceptanceModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if(model.acceptedFiles.Count == 0 || model.FileId == 0)
                return BadRequest("No files accepted");

            var result = await _submissionService.AcceptSubmissions(model);


            return result ? Ok("Submission accepted") : BadRequest("Something failed");
        }
    }
}