using CES.API.Authentication;
using CES.API.Tests.Fixtures;

namespace CES.API.Tests.Authentication;

/// <summary>
/// Reads the identity claims the callback persists off the access token, tolerating tokens
/// that cannot be parsed rather than failing the login.
/// </summary>
public class AccessTokenReaderTests
{
    [Fact]
    public void Read_ExtractsSubjectEmailAndName()
    {
        var token = JwtTokenHelper.KeycloakIdentityToken(
            "sub-123", "bryce.martel@gov.bc.ca", "Bryce", "Martel");

        var claims = AccessTokenReader.Read(token);

        claims.Should().NotBeNull();
        claims!.Subject.Should().Be("sub-123");
        claims.Email.Should().Be("bryce.martel@gov.bc.ca");
        claims.FirstName.Should().Be("Bryce");
        claims.LastName.Should().Be("Martel");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-jwt")]
    [InlineData("still.not.valid")]
    public void Read_ReturnsNullForAnUnreadableToken(string? token)
    {
        AccessTokenReader.Read(token).Should().BeNull();
    }
}
