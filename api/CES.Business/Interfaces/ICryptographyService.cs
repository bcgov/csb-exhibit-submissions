
namespace CES.Business.Interfaces
{
    public interface ICryptographyService
    {
        public Task<string> ComputeSHA256Async(string filePath);
    }
}