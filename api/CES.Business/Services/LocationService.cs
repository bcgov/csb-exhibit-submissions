using CES.Business.Extensions;
using CES.Business.Models.Location;
using JCCommon.Clients.LocationServices;
namespace CES.Business.Services
{
    public class LocationService: ILocationService
    {
        
        private readonly LocationServicesClient _locationClient;

        public LocationService(LocationServicesClient locationServicesClient)
        {
            _locationClient = locationServicesClient;
        }

        //Copied from Jasper
        public async Task<ICollection<Location>> GetJCLocations(bool includeChildRecords)
        {
            var jcLocations = await _locationClient.LocationsGetAsync(null, true, true);
            var locations = new List<Location>();

            foreach(var loc in jcLocations)
            {
                locations.Add(loc.ConvertToLocationModel());
            }

            if (!includeChildRecords)
            {
                return locations;
            }

            var jcCourtRooms = await _locationClient.LocationsRoomsGetAsync();
            var courtRooms = new List<CourtRoom>();

            foreach(var room in jcCourtRooms)
            {
                courtRooms.Add(room.ConvertToCourtRoomModel());
            }

            foreach (var location in locations)
            {
                location.CourtRooms = courtRooms
                    .Where(cr => cr.LocationId == location.LocationId && (cr.Type == "CRT" || cr.Type == "HGR"))
                    .OrderBy(cr => cr.Room).ToList();
            }

            return locations;
        }
    }
}