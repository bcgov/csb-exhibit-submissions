using CES.API.Authentication;
using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CES.API.Controllers
{
    public class LocationsController : Controller
    {
        public ILocationService _locationService {get;set;}
        public ICourtListService _courtListService {get;set;}
        public LocationsController(ILocationService locationService, ICourtListService courtListService) 
        {
            _locationService = locationService;
            _courtListService = courtListService;
        }

        [HttpGet]
        [Route("api/location/getLocations")]
        public async Task<IActionResult> GetLocations()
        {
            var ret = await _locationService.GetJCLocations(true);

            return Ok(ret);
        }

        
        [HttpGet]
        [Route("api/files/getCourtList")]
        public async Task<IActionResult> GetCourtList([FromQuery] string agencyId, string roomCode, string proceedingDate)
        {
            if(string.IsNullOrEmpty(agencyId) || string.IsNullOrEmpty(roomCode) || string.IsNullOrEmpty(proceedingDate))
                return BadRequest("invalid parameters");

            DateTime outProceedingDate;
            if(!DateTime.TryParse(proceedingDate, out outProceedingDate))
                return BadRequest("Invalide date");
            var ret = await _courtListService.GetJCCourtList(agencyId, roomCode, outProceedingDate);

            return Ok(ret);
        }

        
    }
}
