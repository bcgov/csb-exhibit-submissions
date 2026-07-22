using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CES.API.Authentication;
using CES.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace CES.API.Controllers
{
    /// <summary>
    /// Server-mediated Keycloak authorization-code flow. The CES client is confidential,
    /// so the browser can never complete the code exchange — it only ferries the
    /// authorization code here.
    /// <para>
    /// Every endpoint returns 404 when <c>Keycloak:Enabled</c> is false, so the mock
    /// dev-bypass login in <see cref="LoginController"/> remains the only auth path.
    /// </para>
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    [AllowAnonymous]
    public class AuthController : ControllerBase
    {
        private readonly KeycloakConfiguration _keycloak;
        private readonly IKeycloakTokenService _tokenService;
        private readonly IDataProtector _loginStateProtector;
        private readonly IDataProtector _sessionProtector;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            KeycloakConfiguration keycloak,
            IKeycloakTokenService tokenService,
            IDataProtectionProvider dataProtectionProvider,
            ILogger<AuthController> logger)
        {
            _keycloak = keycloak;
            _tokenService = tokenService;
            _loginStateProtector = dataProtectionProvider.CreateProtector(
                AuthConstants.LoginStateProtectorPurpose);
            _sessionProtector = dataProtectionProvider.CreateProtector(
                AuthConstants.SessionProtectorPurpose);
            _logger = logger;
        }

        /// <summary>
        /// Starts the flow: generates state + PKCE, stores them in the encrypted
        /// <c>ces.login</c> cookie, and redirects the browser to Keycloak.
        /// </summary>
        [HttpGet("login")]
        public async Task<IActionResult> Login([FromQuery] string? returnUrl, CancellationToken ct)
        {
            if (!_keycloak.Enabled)
                return NotFound();

            var (authorizeUrl, loginState) = await _tokenService.BuildAuthorizeRequestAsync(returnUrl, ct);

            WriteCookie(
                AuthConstants.LoginStateCookieName,
                _loginStateProtector.Protect(JsonSerializer.Serialize(loginState)),
                TimeSpan.FromMinutes(AuthConstants.LoginStateLifetimeMinutes));

            // A full-page redirect, not an API response: the browser has to follow this
            // all the way to the IDIR login screen.
            return Redirect(authorizeUrl);
        }

        /// <summary>
        /// Completes the flow. <c>AuthCallback.vue</c> posts the code here; this is the only
        /// party that may authenticate to Keycloak's token endpoint.
        /// </summary>
        [HttpPost("callback")]
        public async Task<IActionResult> Callback(
            [FromBody] AuthCallbackRequest request, CancellationToken ct)
        {
            if (!_keycloak.Enabled)
                return NotFound();

            if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.State))
                throw new ArgumentException("Both an authorization code and a state value are required.");

            var loginState = ReadLoginStateCookie();

            // Single-use: clear the cookie before the exchange so a replayed state cannot
            // be validated a second time, whatever the exchange does next.
            DeleteCookie(AuthConstants.LoginStateCookieName);

            if (loginState.IsExpired(DateTimeOffset.UtcNow))
                throw new ArgumentException("The login attempt expired. Please sign in again.");

            if (!FixedTimeEquals(loginState.State, request.State))
            {
                _logger.LogWarning("Rejected an auth callback whose state did not match the login cookie.");
                throw new ArgumentException("The login state did not match. Please sign in again.");
            }

            var tokens = await _tokenService.ExchangeCodeAsync(request.Code, loginState.CodeVerifier, ct);

            WriteSessionCookie(tokens);

            return Ok(new AuthCallbackResponse
            {
                AccessToken = tokens.AccessToken,
                ExpiresIn = tokens.ExpiresIn,
                ReturnUrl = loginState.ReturnUrl,
            });
        }

        private LoginState ReadLoginStateCookie()
        {
            var cookie = Request.Cookies[AuthConstants.LoginStateCookieName];
            if (string.IsNullOrEmpty(cookie))
                throw new ArgumentException("No login attempt is in progress. Please sign in again.");

            try
            {
                return JsonSerializer.Deserialize<LoginState>(_loginStateProtector.Unprotect(cookie))
                    ?? throw new ArgumentException("The login cookie could not be read. Please sign in again.");
            }
            catch (Exception ex) when (ex is CryptographicException or JsonException)
            {
                // Tampered, or encrypted under a key ring this instance cannot read.
                _logger.LogWarning("Failed to unprotect the login state cookie.");
                throw new ArgumentException("The login cookie could not be read. Please sign in again.");
            }
        }

        /// <summary>
        /// Writes the refresh token and id_token into the encrypted, HttpOnly session cookie.
        /// Neither value is ever returned to the browser in a response body.
        /// </summary>
        private void WriteSessionCookie(KeycloakTokenResponse tokens)
        {
            var session = new SessionState
            {
                RefreshToken = tokens.RefreshToken ?? string.Empty,
                IdToken = tokens.IdToken,
                IssuedAtUtc = DateTimeOffset.UtcNow,
            };

            WriteCookie(
                AuthConstants.SessionCookieName,
                _sessionProtector.Protect(JsonSerializer.Serialize(session)),
                TimeSpan.FromHours(AuthConstants.SessionCookieLifetimeHours));
        }

        private void WriteCookie(string name, string value, TimeSpan maxAge) =>
            Response.Cookies.Append(name, value, BuildCookieOptions(maxAge));

        private void DeleteCookie(string name) =>
            Response.Cookies.Delete(name, BuildCookieOptions(maxAge: null));

        private static CookieOptions BuildCookieOptions(TimeSpan? maxAge) => new()
        {
            HttpOnly = true,
            // localhost is a secure context, so this still works in local development.
            Secure = true,
            // Lax, not Strict: the ces.login cookie must survive Keycloak's top-level
            // redirect back to /auth/callback.
            SameSite = SameSiteMode.Lax,
            // Scoped so the browser never attaches the refresh token to /api/submissions
            // or the large multipart uploads on /api/files.
            Path = AuthConstants.AuthCookiePath,
            IsEssential = true,
            MaxAge = maxAge,
        };

        private static bool FixedTimeEquals(string expected, string actual) =>
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(actual));
    }
}
