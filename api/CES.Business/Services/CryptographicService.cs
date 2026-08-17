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
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            return await ComputeSHA256Async(stream);
        }

        // Hashes whatever the stream yields from its current position to the end.
        // The caller owns the stream (and its position): a remote store has no file
        // path to hand to FileStream, and the pending/accepted verification reads a
        // stream it may need to rewind and reuse.
        public static async Task<string> ComputeSHA256Async(Stream stream)
        {
            using var sha = SHA256.Create();

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