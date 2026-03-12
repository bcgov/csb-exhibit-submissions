using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Entities.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CES.API.Controllers
{
    public class FilesController : Controller
    {

        private readonly IFileStorage _fileStorage;
        private IFileService _fileService;

        public FilesController(IFileStorage fileStorage, IFileService fileService)
        {
            _fileStorage = fileStorage;
            _fileService = fileService;
        }

        [HttpGet]
        [Route("api/files/{fileId}/view")]
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
        public async Task<IActionResult> Download(Guid fileId)
        {
            var file = await _fileService.RetrieveFileMetaData(fileId);

            if (file == null)
                return NotFound();

            var stream = await _fileStorage.GetAsync(file);

            return File(stream, file.ContentType, file.OriginalFileName);
        }
    }
}