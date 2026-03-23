using System.Security.Cryptography;
using System.Text;
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
        

        public static async Task<string> GenerateVideoViewToken(Guid fileId, DateTime expires, string hashKey = "thisneedstobesomethingthatisatleastafewcharacterslong")
        {
            var payload = $"{fileId}|{expires:o}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(hashKey));
            var hash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));

            return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{payload}|{hash}"));
        }

        public static async Task<bool> ValidateVideoToken(Guid fileId, string token, string hashKey = "thisneedstobesomethingthatisatleastafewcharacterslong")
        {
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                var parts = decoded.Split('|');

                var id = Guid.Parse(parts[0]);
                var expires = DateTime.Parse(parts[1]);
                var hash = parts[2];

                if (id != fileId || expires < DateTime.UtcNow)
                    return false;

                var payload = $"{id}|{expires:o}";

                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(hashKey));
                var expectedHash = Convert.ToBase64String(
                    hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))
                );

                return hash == expectedHash;
            }
            catch
            {
                return false;
            }
        }
    }
}