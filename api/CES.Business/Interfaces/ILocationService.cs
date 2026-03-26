
using CES.Business.Models.Location;

namespace CES.Business.Services
{
    public interface ILocationService
    {
        public Task<ICollection<Location>> GetJCLocations(bool includeChildRecords);
    }
}