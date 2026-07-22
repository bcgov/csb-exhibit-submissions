using System.Net;
using System.Net.Http.Json;
using CES.API.Authentication;
using CES.API.Tests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Net.Http.Headers;

namespace CES.API.Tests.Controllers;

/// <summary>
/// AuthController with Keycloak enabled. Cookies are handled manually rather than by a
/// CookieContainer: they are marked Secure, which the container refuses to replay over
/// the test server's http origin (browsers make an exception for localhost, .NET does not).
/// </summary>
public class AuthControllerTests : IClassFixture<KeycloakTestWebApplicationFactory>
{
    private readonly KeycloakTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthControllerTests(KeycloakTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
    }

    // ---------- GET /api/auth/login ----------

    [Fact]
    public async Task Login_RedirectsToKeycloaksAuthorizeUrl()
    {
        var response = await _client.GetAsync("/api/auth/login");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be(FakeKeycloakTokenService.AuthorizeUrl);
    }

    [Fact]
    public async Task Login_SetsTheLoginCookieWithAllFourSecurityAttributes()
    {
        var response = await _client.GetAsync("/api/auth/login");

        var cookie = SetCookieHeaderValue.Parse(
            FindSetCookie(response, AuthConstants.LoginStateCookieName));

        cookie.HttpOnly.Should().BeTrue();
        cookie.Secure.Should().BeTrue();
        cookie.SameSite.Should().Be(SameSiteMode.Lax);
        cookie.Path.Value.Should().Be(AuthConstants.AuthCookiePath);
    }

    [Fact]
    public async Task Login_LoginCookieValueIsOpaqueNotReadablePlaintext()
    {
        var response = await _client.GetAsync("/api/auth/login");

        var value = CookieValue(response, AuthConstants.LoginStateCookieName);

        // Data Protection-encrypted: neither the state nor the PKCE verifier is recoverable
        // by anything that can read the cookie jar.
        value.Should().NotContain(FakeKeycloakTokenService.State);
        value.Should().NotContain(FakeKeycloakTokenService.CodeVerifier);
    }

    [Fact]
    public async Task Login_CarriesTheReturnUrlInTheCookieNotTheRedirectUrl()
    {
        var response = await _client.GetAsync("/api/auth/login?returnUrl=%2Fofficer%2Fcourt-list");

        // The return URL must not ride in the browser-visible URL.
        response.Headers.Location!.ToString().Should().NotContain("court-list");
        CookieValue(response, AuthConstants.LoginStateCookieName).Should().NotContain("court-list");
    }

    // ---------- POST /api/auth/callback ----------

    [Fact]
    public async Task Callback_OnSuccess_ReturnsTheAccessTokenAndSanitizedReturnUrl()
    {
        var loginCookie = await StartLoginAsync("/officer/court-list");

        var response = await PostCallbackAsync(loginCookie, FakeKeycloakTokenService.State);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CallbackBody>();
        body!.AccessToken.Should().Be(FakeKeycloakTokenService.AccessToken);
        body.ExpiresIn.Should().Be(FakeKeycloakTokenService.ExpiresIn);
        body.ReturnUrl.Should().Be("/officer/court-list");
    }

    [Fact]
    public async Task Callback_WithAnOffSiteReturnUrl_ComesBackWithTheAppRoot()
    {
        var loginCookie = await StartLoginAsync("//evil.example");

        var response = await PostCallbackAsync(loginCookie, FakeKeycloakTokenService.State);

        var body = await response.Content.ReadFromJsonAsync<CallbackBody>();
        body!.ReturnUrl.Should().Be(AuthConstants.DefaultReturnUrl);
    }

    [Fact]
    public async Task Callback_ResponseBodyNeverContainsTheRefreshToken()
    {
        var loginCookie = await StartLoginAsync();

        var response = await PostCallbackAsync(loginCookie, FakeKeycloakTokenService.State);
        var raw = await response.Content.ReadAsStringAsync();

        raw.Should().NotContain(FakeKeycloakTokenService.RefreshToken);
        raw.Should().NotContain(FakeKeycloakTokenService.IdToken);
    }

    [Fact]
    public async Task Callback_SetsTheSessionCookieWithAllFourSecurityAttributes()
    {
        var loginCookie = await StartLoginAsync();

        var response = await PostCallbackAsync(loginCookie, FakeKeycloakTokenService.State);

        var cookie = SetCookieHeaderValue.Parse(
            FindSetCookie(response, AuthConstants.SessionCookieName));

        cookie.HttpOnly.Should().BeTrue();
        cookie.Secure.Should().BeTrue();
        cookie.SameSite.Should().Be(SameSiteMode.Lax);
        cookie.Path.Value.Should().Be(AuthConstants.AuthCookiePath);
    }

    [Fact]
    public async Task Callback_SessionCookieDoesNotExposeTheRefreshToken()
    {
        var loginCookie = await StartLoginAsync();

        var response = await PostCallbackAsync(loginCookie, FakeKeycloakTokenService.State);

        CookieValue(response, AuthConstants.SessionCookieName)
            .Should().NotContain(FakeKeycloakTokenService.RefreshToken);
    }

    [Fact]
    public async Task Callback_ClearsTheLoginCookieSoTheStateIsSingleUse()
    {
        var loginCookie = await StartLoginAsync();

        var response = await PostCallbackAsync(loginCookie, FakeKeycloakTokenService.State);

        // Deleting a cookie is an expiry in the past, not the absence of a header.
        var cleared = SetCookieHeaderValue.Parse(
            FindSetCookie(response, AuthConstants.LoginStateCookieName));

        cleared.Value.Value.Should().BeEmpty();
        cleared.Expires.Should().BeBefore(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Callback_PassesThePkceVerifierFromTheCookieToTheExchange()
    {
        var loginCookie = await StartLoginAsync();

        await PostCallbackAsync(loginCookie, FakeKeycloakTokenService.State);

        _factory.TokenService.ObservedCodeVerifier
            .Should().Be(FakeKeycloakTokenService.CodeVerifier);
    }

    [Fact]
    public async Task Callback_WithAMismatchedState_Returns400AndDoesNotExchange()
    {
        var loginCookie = await StartLoginAsync();
        var before = _factory.TokenService.ExchangeCallCount;

        var response = await PostCallbackAsync(loginCookie, "not-the-right-state");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _factory.TokenService.ExchangeCallCount.Should().Be(before);
    }

    [Fact]
    public async Task Callback_WithNoLoginCookie_Returns400()
    {
        var response = await PostCallbackAsync(loginCookie: null, FakeKeycloakTokenService.State);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Callback_WithATamperedLoginCookie_Returns400()
    {
        var loginCookie = await StartLoginAsync();
        var tampered = "A" + loginCookie[1..];

        var response = await PostCallbackAsync(tampered, FakeKeycloakTokenService.State);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Callback_WithAMissingCode_Returns400()
    {
        var loginCookie = await StartLoginAsync();

        var response = await PostCallbackAsync(loginCookie, FakeKeycloakTokenService.State, code: "");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- Helpers ----------

    private async Task<string> StartLoginAsync(string? returnUrl = null)
    {
        var url = returnUrl is null
            ? "/api/auth/login"
            : $"/api/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}";

        var response = await _client.GetAsync(url);
        return CookieValue(response, AuthConstants.LoginStateCookieName);
    }

    private async Task<HttpResponseMessage> PostCallbackAsync(
        string? loginCookie, string state, string code = "the-authorization-code")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/callback")
        {
            Content = JsonContent.Create(new { code, state }),
        };

        if (loginCookie is not null)
        {
            request.Headers.Add(
                HeaderNames.Cookie,
                $"{AuthConstants.LoginStateCookieName}={loginCookie}");
        }

        return await _client.SendAsync(request);
    }

    private static string FindSetCookie(HttpResponseMessage response, string name) =>
        response.Headers.GetValues(HeaderNames.SetCookie)
            .FirstOrDefault(h => h.StartsWith($"{name}=", StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"No Set-Cookie header found for '{name}'.");

    private static string CookieValue(HttpResponseMessage response, string name) =>
        SetCookieHeaderValue.Parse(FindSetCookie(response, name)).Value.Value;

    private record CallbackBody(string AccessToken, int ExpiresIn, string ReturnUrl);
}

/// <summary>
/// The dev-bypass guarantee: with Keycloak:Enabled false the new endpoints are invisible
/// and the mock login is untouched.
/// </summary>
public class AuthControllerDisabledTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthControllerDisabledTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    [Fact]
    public async Task Login_WhenKeycloakIsDisabled_Returns404()
    {
        var response = await _client.GetAsync("/api/auth/login");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Callback_WhenKeycloakIsDisabled_Returns404()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/callback", new
        {
            code = "the-code",
            state = "the-state",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Login_WhenKeycloakIsDisabled_LeavesNoAuthCookiesBehind()
    {
        var response = await _client.GetAsync("/api/auth/login");

        response.Headers.TryGetValues(HeaderNames.SetCookie, out var cookies);
        (cookies ?? []).Should().NotContain(c =>
            c.StartsWith(AuthConstants.LoginStateCookieName, StringComparison.Ordinal) ||
            c.StartsWith(AuthConstants.SessionCookieName, StringComparison.Ordinal));
    }

    [Fact]
    public async Task MockLogin_StillWorksOnTheSamePathWithPost()
    {
        // POST /api/auth/login (mock) and GET /api/auth/login (Keycloak) coexist by verb.
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "officer@gov.bc.ca",
            password = "pass123",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
