using System.Net;
using System.Net.Http.Json;
using CES.API.Tests.Fixtures;
using FluentAssertions;

namespace CES.API.Tests.Controllers;

public class LoginControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public LoginControllerTests(TestWebApplicationFactory factory)
    {
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
    public async Task Login_WithInvalidCredentials_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "unknown@gov.bc.ca",
            password = "wrong"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private record TokenResponse(string Token);
}
