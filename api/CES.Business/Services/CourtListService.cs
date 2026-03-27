using CES.Business.Extensions;
using CES.Business.Extensions.Entities;
using CES.Business.Models.Location;
using JCCommon.Clients.FileServices;
using JCCommon.Clients.LocationServices;
using Microsoft.Extensions.Configuration;
namespace CES.Business.Services
{
    public class CourtListService: ICourtListService
    {
        private readonly FileServicesClient _filesClient;
        private readonly string _applicationCode;
        private readonly string _requestAgencyIdentifierId;
        private readonly string _requestPartId;

        public CourtListService(FileServicesClient fileServicesClient,
            IConfiguration configuration)
        {
            _filesClient = fileServicesClient;
            _applicationCode = configuration.GetNonEmptyValue("Request:ApplicationCd");
            _requestAgencyIdentifierId = configuration.GetNonEmptyValue("Request:AgencyIdentifierId");
            _requestPartId = configuration.GetNonEmptyValue("Request:PartId");
        }

        //Copied from Jasper
        public async Task<ICollection<CES.Business.Models.Location.CourtList>> GetJCCourtList(string agencyId, string roomCode, DateTime proceeding)
        {
            var proceedingDateString = proceeding.ToString("yyyy-MM-dd");
            var courtListJC =  await _filesClient.FilesCourtlistAsync(_requestAgencyIdentifierId, _requestPartId, _applicationCode, agencyId, roomCode, proceedingDateString, null, null);
            
            if(courtListJC.CriminalCourtList.Count() == 0)
                return new List<Models.Location.CourtList>();

            var list = new List<Models.Location.CourtList>();
            foreach(var file in courtListJC.CriminalCourtList)
            {
                var entity = file.ToLocalEntity();
                list.Add(entity);
            }

            return list;
            
        }
    }
}