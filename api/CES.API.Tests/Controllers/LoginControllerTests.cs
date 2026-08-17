using System.Net;
using System.Net.Http.Json;
using CES.API.Tests.Fixtures;
using CES.EF;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CES.API.Tests.Controllers;

public class LoginControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LoginControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithToken()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "admin@gov.bc.ca",
            password = "pass123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        body!.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithClerkCredentials_Returns200WithToken()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "clerk@gov.bc.ca",
            password = "pass123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        body!.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "unknown@gov.bc.ca",
            password = "wrong"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_ProvisionsTheLocalUserRow_SoAuditWritesCanLinkToIt()
    {
        await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "officer@gov.bc.ca",
            password = "pass123"
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CESDataStore>();

        var row = db.ApplicationUser.Single(user => user.Email == "officer@gov.bc.ca");
        row.IsActive.Should().BeTrue();
        // A dev-bypass account has no realm subject; it is matched on email instead.
        row.KeycloakSub.Should().BeNull();
    }

    [Fact]
    public async Task Login_Twice_DoesNotDuplicateTheLocalUserRow()
    {
        var credentials = new { username = "clerk@gov.bc.ca", password = "pass123" };

        await _client.PostAsJsonAsync("/api/auth/login", credentials);
        await _client.PostAsJsonAsync("/api/auth/login", credentials);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CESDataStore>();

        db.ApplicationUser.Count(user => user.Email == "clerk@gov.bc.ca").Should().Be(1);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ProvisionsNothing()
    {
        await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "intruder@gov.bc.ca",
            password = "pass123"
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CESDataStore>();

        db.ApplicationUser.Should().NotContain(user => user.Email == "intruder@gov.bc.ca");
    }

    private record TokenResponse(string Token);
}
