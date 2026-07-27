using System.Net;
using CES.API.Authentication;
using CES.API.Tests.Fixtures;
using CES.EF;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace CES.API.Tests.Controllers;

/// <summary>
/// Refresh, logout, and the local-user upsert. A fresh factory per test keeps the mutable
/// fake token service and the in-memory user table isolated between cases.
/// </summary>
public class AuthControllerSessionTests : IDisposable
{
    private readonly KeycloakTestWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public AuthControllerSessionTests()
    {
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ---------- POST /api/auth/refresh ----------

    [Fact]
    public async Task Refresh_WithAValidSessionCookie_ReturnsANewAccessToken()
    {
        var sessionCookie = await SignInAsync();

        var response = await PostRefreshAsync(sessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RefreshBody>();
        body!.AccessToken.Should().Be(FakeKeycloakTokenService.AccessToken);
        body.ExpiresIn.Should().Be(FakeKeycloakTokenService.ExpiresIn);
    }

    [Fact]
    public async Task Refresh_ReadsTheRefreshTokenOutOfTheCookieNotTheBody()
    {
        var sessionCookie = await SignInAsync();

        await PostRefreshAsync(sessionCookie);

        _factory.TokenService.ObservedRefreshToken.Should().Be(FakeKeycloakTokenService.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WritesTheRotatedRefreshTokenBackIntoTheCookie()
    {
        // Prove rotation end-to-end: the second refresh must present the token the first
        // refresh rotated in, which can only happen if the cookie was re-issued.
        var sessionCookie = await SignInAsync();

        var firstRefresh = await PostRefreshAsync(sessionCookie);
        var rotatedCookie = CookieValue(firstRefresh, AuthConstants.SessionCookieName);

        await PostRefreshAsync(rotatedCookie);

        _factory.TokenService.ObservedRefreshToken.Should().Be(FakeKeycloakTokenService.RotatedRefreshToken);
    }

    [Fact]
    public async Task Refresh_ResponseBodyNeverContainsARefreshToken()
    {
        var sessionCookie = await SignInAsync();

        var response = await PostRefreshAsync(sessionCookie);
        var raw = await response.Content.ReadAsStringAsync();

        raw.Should().NotContain(FakeKeycloakTokenService.RefreshToken);
        raw.Should().NotContain(FakeKeycloakTokenService.RotatedRefreshToken);
    }

    [Fact]
    public async Task Refresh_WithNoSessionCookie_Returns401()
    {
        var response = await PostRefreshAsync(sessionCookie: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WhenKeycloakRejectsTheGrant_Returns401AndClearsTheCookie()
    {
        var sessionCookie = await SignInAsync();
        _factory.TokenService.RejectRefresh = true;

        var response = await PostRefreshAsync(sessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        AssertCookieCleared(response, AuthConstants.SessionCookieName);
    }

    // ---------- id_token survival across a refresh ----------

    [Fact]
    public async Task Refresh_WhenTheGrantOmitsAnIdToken_CarriesTheExistingOneForwardToLogout()
    {
        // Keycloak's refresh grant need not return an id_token; dropping it would leave
        // logout with no id_token_hint after the very first renewal.
        _factory.TokenService.RefreshReturnsNoIdToken = true;
        var sessionCookie = await SignInAsync();

        var refreshed = await PostRefreshAsync(sessionCookie);
        var refreshedCookie = CookieValue(refreshed, AuthConstants.SessionCookieName);

        await PostLogoutAsync(refreshedCookie);

        _factory.TokenService.ObservedLogoutIdToken.Should().Be(FakeKeycloakTokenService.IdToken);
    }

    // ---------- POST /api/auth/logout ----------

    [Fact]
    public async Task Logout_ClearsTheSessionCookieAndReturnsTheEndSessionUrl()
    {
        var sessionCookie = await SignInAsync();

        var response = await PostLogoutAsync(sessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LogoutBody>();
        body!.EndSessionUrl.Should().Be(FakeKeycloakTokenService.EndSessionUrl);
        AssertCookieCleared(response, AuthConstants.SessionCookieName);
    }

    [Fact]
    public async Task Logout_PassesTheIdTokenHintFromTheCookie()
    {
        var sessionCookie = await SignInAsync();

        await PostLogoutAsync(sessionCookie);

        _factory.TokenService.ObservedLogoutIdToken.Should().Be(FakeKeycloakTokenService.IdToken);
    }

    [Fact]
    public async Task Logout_WhenTheEndSessionUrlCannotBeBuilt_StillClearsTheCookie()
    {
        // A Keycloak outage must never leave the browser holding a live refresh token.
        var sessionCookie = await SignInAsync();
        _factory.TokenService.RejectEndSession = true;

        var response = await PostLogoutAsync(sessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertCookieCleared(response, AuthConstants.SessionCookieName);
    }

    [Fact]
    public async Task Logout_WithNoCookie_StillSucceeds()
    {
        var response = await PostLogoutAsync(sessionCookie: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------- callback upserts the local user ----------

    [Fact]
    public async Task Callback_UpsertsTheLocalUserFromTheAccessToken()
    {
        const string sub = "827df02f-d284-49a9-84b0-b4893c107cb5";
        _factory.TokenService.AccessTokenOverride = JwtTokenHelper.KeycloakIdentityToken(
            sub, "bryce.martel@gov.bc.ca", "Bryce", "Martel");

        await SignInAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CESDataStore>();
        var row = db.ApplicationUser.Single(user => user.KeycloakSub == sub);
        row.Email.Should().Be("bryce.martel@gov.bc.ca");
        row.FirstName.Should().Be("Bryce");
    }

    [Fact]
    public async Task Callback_WhenTheAccessTokenHasNoReadableSubject_StillSignsIn()
    {
        // The default fake access token is not a JWT: the upsert is skipped, but the login
        // must not fail — authorization comes entirely from the token, not this row.
        var response = await CallbackAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CESDataStore>();
        db.ApplicationUser.Should().BeEmpty();
    }

    // ---------- helpers ----------

    /// <summary>Runs login + callback and returns the freshly issued session-cookie value.</summary>
    private async Task<string> SignInAsync(string? returnUrl = null)
    {
        var callback = await CallbackAsync(returnUrl);
        return CookieValue(callback, AuthConstants.SessionCookieName);
    }

    private async Task<HttpResponseMessage> CallbackAsync(string? returnUrl = null)
    {
        var url = returnUrl is null
            ? "/api/auth/login"
            : $"/api/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}";

        var login = await _client.GetAsync(url);
        var loginCookie = CookieValue(login, AuthConstants.LoginStateCookieName);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/callback")
        {
            Content = JsonContent.Create(new { code = "the-code", state = FakeKeycloakTokenService.State }),
        };
        request.Headers.Add(HeaderNames.Cookie, $"{AuthConstants.LoginStateCookieName}={loginCookie}");

        return await _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostRefreshAsync(string? sessionCookie) =>
        PostToAuthAsync("/api/auth/refresh", sessionCookie);

    private Task<HttpResponseMessage> PostLogoutAsync(string? sessionCookie) =>
        PostToAuthAsync("/api/auth/logout", sessionCookie);

    private Task<HttpResponseMessage> PostToAuthAsync(string path, string? sessionCookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);

        if (sessionCookie is not null)
        {
            request.Headers.Add(
                HeaderNames.Cookie,
                $"{AuthConstants.SessionCookieName}={sessionCookie}");
        }

        return _client.SendAsync(request);
    }

    private static void AssertCookieCleared(HttpResponseMessage response, string name)
    {
        var cleared = SetCookieHeaderValue.Parse(FindSetCookie(response, name));
        cleared.Value.Value.Should().BeEmpty();
        cleared.Expires.Should().BeBefore(DateTimeOffset.UtcNow);
    }

    private static string FindSetCookie(HttpResponseMessage response, string name) =>
        response.Headers.GetValues(HeaderNames.SetCookie)
            .FirstOrDefault(h => h.StartsWith($"{name}=", StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"No Set-Cookie header found for '{name}'.");

    private static string CookieValue(HttpResponseMessage response, string name) =>
        SetCookieHeaderValue.Parse(FindSetCookie(response, name)).Value.Value;

    private record RefreshBody(string AccessToken, int ExpiresIn);
    private record LogoutBody(string EndSessionUrl);
}
