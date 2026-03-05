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
                                                                Content = f.OpenReadStream()
                                                            }).ToList();

            if(model.fileUploads.Count == 0)
                return BadRequest("No files uploaded");

            var result = await _submissionService.SubmitEvidence(model);


            return result ? Ok("Submission accepted") : BadRequest("Something failed");
        }
    }
}