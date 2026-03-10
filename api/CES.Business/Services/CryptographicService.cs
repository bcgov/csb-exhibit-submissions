using System.Security.Cryptography;
using CES.Business.Interfaces;

namespace CES.Business.Services
{
    public class CryptographyService//: ICryptographyService
    {
        public CryptographyService()
        {}

        public static async Task<string> ComputeSHA256Async(string filePath)
        {
            using var sha = SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            var hash = await sha.ComputeHashAsync(stream);

            return Convert.ToHexString(hash);
        }
    }
}