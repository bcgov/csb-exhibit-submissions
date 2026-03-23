
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CES.API;
using CES.API.Authentication;
using Microsoft.IdentityModel.Tokens;

namespace CES.Business.Infrastructure
{
    // A temporary class for generating JWT tokens.  Intended to be deleted
    public class LocalTokenService : ITokenService
    {
        private readonly IConfiguration _config;

        public LocalTokenService(IConfiguration config)
        {
            _config = config;
        }

        public string GenerateToken(string username, string role = "User")
        {
            var authSettings = _config.GetSection("UserAuth").Get<AuthConfiguration>();
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authSettings?.Key!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

            var token = new JwtSecurityToken(
                issuer: authSettings?.Issuer,
                audience: authSettings?.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}