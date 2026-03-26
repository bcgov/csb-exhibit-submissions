
using CES.Business.Models.Location;

namespace CES.Business.Services
{
    public interface ICourtListService
    {
        public  Task<ICollection<Models.Location.CourtList>> GetJCCourtList(string agencyId, string roomCode, DateTime proceeding);
    }
}