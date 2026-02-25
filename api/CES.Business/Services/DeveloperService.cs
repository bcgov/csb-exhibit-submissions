using CES.Business.Interfaces;

namespace CES.Business.Services
{
    public class DeveloperService : IDeveloperService
    {
        public bool HealthCheck()
        {
            return true;
        }
    }
}