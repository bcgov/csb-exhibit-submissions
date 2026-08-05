using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CES.API.Tests.Fixtures;
using CES.Business.Models;
using FluentAssertions;

namespace CES.API.Tests.Controllers;

/// <summary>
/// The signed-in user's own profile (CES-27). The officer number is not an IDIR claim, so
/// these endpoints are the only route it takes into and out of CES.
/// </summary>
public class UserControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UserControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private HttpClient WithAuth(string token)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return _client;
    }

    [Fact]
    public async Task GetProfile_WithoutAToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateOfficerNumber_WithoutAToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PutAsJsonAsync(
            "/api/users/me/officer-number", new OfficerNumberUpdateModel { OfficerNumber = "PC1234" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProfile_ReturnsTheCallersOwnRow()
    {
        var response = await WithAuth(JwtTokenHelper.UserToken()).GetAsync("/api/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileModel>();
        profile.Should().NotBeNull();
        // Resolved from the token's subject, never from a parameter the caller controls.
        profile!.Email.Should().Be("officer@gov.bc.ca");
    }

    [Fact]
    public async Task UpdateOfficerNumber_PersistsAndIsReadBackByGetProfile()
    {
        WithAuth(JwtTokenHelper.UserToken());

        var update = await _client.PutAsJsonAsync(
            "/api/users/me/officer-number", new OfficerNumberUpdateModel { OfficerNumber = "PC-1234" });

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        (await update.Content.ReadFromJsonAsync<UserProfileModel>())!
            .OfficerNumber.Should().Be("PC-1234");

        var profile = await (await _client.GetAsync("/api/users/me"))
            .Content.ReadFromJsonAsync<UserProfileModel>();
        profile!.OfficerNumber.Should().Be("PC-1234");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("PC 1234")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]  // 31 chars, one over the maximum
    public async Task UpdateOfficerNumber_WithAnInvalidValue_Returns400(string? officerNumber)
    {
        WithAuth(JwtTokenHelper.UserToken());

        var response = await _client.PutAsJsonAsync(
            "/api/users/me/officer-number", new OfficerNumberUpdateModel { OfficerNumber = officerNumber });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetProfile_IsAvailableToAdmins_WhoSimplyHaveNoOfficerNumber()
    {
        // Not officer-gated: the endpoint is a generic "who am I", and Admin/Clerk rows are
        // resolved the same way — they just never carry a number.
        var response = await WithAuth(JwtTokenHelper.AdminToken()).GetAsync("/api/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<UserProfileModel>())!
            .OfficerNumber.Should().BeNull();
    }
}
