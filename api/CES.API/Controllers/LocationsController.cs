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
        public LocationsController(ILocationService locationService) 
        {
            _locationService = locationService;
        }

        [HttpPost]
        [Route("api/location/getLocations")]
        public IActionResult LoginUser([FromBody] CESLoginModel model)
        {
            var ret = _locationService.GetJCLocations(true);

            return Ok(ret);
        }

        
    }
}
