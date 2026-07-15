using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace CES.API.Tests.Fixtures;

public static class JwtTokenHelper
{
    private const string Key = "thisismysecretkeytherearemanylikeitbutthisoneismine";
    private const string Issuer = "CES-PoC-Local";
    private const string Audience = "EvidenceSubmission-User";

    public static string GenerateToken(string username, string role)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim("role", role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string AdminToken() => GenerateToken("admin@gov.bc.ca", "Admin");
    public static string UserToken() => GenerateToken("officer@gov.bc.ca", "User");
    public static string ClerkToken() => GenerateToken("clerk@gov.bc.ca", "Clerk");
}
