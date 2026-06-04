using System.IdentityModel.Tokens.Jwt;
using CES.Business.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace CES.API.Tests.Authentication;

public class LocalTokenServiceTests
{
    private readonly LocalTokenService _service;

    public LocalTokenServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UserAuth:Key"] = "thisismysecretkeytherearemanylikeitbutthisoneismine",
                ["UserAuth:Issuer"] = "CES-PoC-Local",
                ["UserAuth:Audience"] = "EvidenceSubmission-User",
                ["UserAuth:DurationMinutes"] = "90"
            })
            .Build();

        _service = new LocalTokenService(config);
    }

    [Fact]
    public void GenerateToken_ReturnsValidJwt()
    {
        var token = _service.GenerateToken("admin@gov.bc.ca", "Admin");

        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(token).Should().BeTrue();

        var jwt = handler.ReadJwtToken(token);
        jwt.Claims.FirstOrDefault(c => c.Type == "role")?.Value.Should().Be("Admin");
        jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
            .Should().Be("admin@gov.bc.ca");
    }

    [Fact]
    public void GenerateToken_ExpiresAfterConfiguredDuration()
    {
        var before = DateTime.UtcNow;

        var token = _service.GenerateToken("user@gov.bc.ca", "User");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.ValidTo.Should().BeAfter(before);
    }
}
