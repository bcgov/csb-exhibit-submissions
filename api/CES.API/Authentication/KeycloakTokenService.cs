using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace CES.API.Authentication
{
    /// <summary>
    /// Server-side half of the Authorization Code + PKCE flow. The client is confidential,
    /// so the code exchange and every refresh must happen here — the secret can never be
    /// shipped to the SPA.
    /// </summary>
    public class KeycloakTokenService : IKeycloakTokenService
    {
        private readonly HttpClient _httpClient;
        private readonly KeycloakConfiguration _keycloak;
        private readonly IConfigurationManager<OpenIdConnectConfiguration> _discovery;
        private readonly ILogger<KeycloakTokenService> _logger;

        public KeycloakTokenService(
            HttpClient httpClient,
            KeycloakConfiguration keycloak,
            IConfigurationManager<OpenIdConnectConfiguration> discovery,
            ILogger<KeycloakTokenService> logger)
        {
            _httpClient = httpClient;
            _keycloak = keycloak;
            _discovery = discovery;
            _logger = logger;
        }

        public async Task<(string AuthorizeUrl, LoginState State)> BuildAuthorizeRequestAsync(
            string? returnUrl, CancellationToken ct)
        {
            var configuration = await _discovery.GetConfigurationAsync(ct);

            var codeVerifier = CreateSecureRandomString();
            var state = CreateSecureRandomString();

            var authorizeUrl = QueryHelpers.AddQueryString(
                configuration.AuthorizationEndpoint,
                new Dictionary<string, string?>
                {
                    [AuthConstants.OAuth.ClientId] = _keycloak.Client,
                    [AuthConstants.OAuth.ResponseType] = AuthConstants.OAuth.ResponseTypeCode,
                    // Sent from configuration, never reconstructed from the incoming request:
                    // the token exchange must repeat this value byte-identically.
                    [AuthConstants.OAuth.RedirectUri] = _keycloak.RedirectUri,
                    [AuthConstants.OAuth.Scope] = AuthConstants.Scopes,
                    [AuthConstants.OAuth.State] = state,
                    [AuthConstants.OAuth.CodeChallenge] = CreateCodeChallenge(codeVerifier),
                    [AuthConstants.OAuth.CodeChallengeMethod] = AuthConstants.OAuth.CodeChallengeMethodS256,
                    [AuthConstants.OAuth.IdpHintParameter] = AuthConstants.IdpHint,
                });

            var loginState = new LoginState
            {
                State = state,
                CodeVerifier = codeVerifier,
                ReturnUrl = ReturnUrlValidator.Sanitize(returnUrl),
                IssuedAtUtc = DateTimeOffset.UtcNow,
            };

            return (authorizeUrl, loginState);
        }

        public async Task<KeycloakTokenResponse> ExchangeCodeAsync(
            string code, string codeVerifier, CancellationToken ct)
        {
            return await PostToTokenEndpointAsync(new Dictionary<string, string>
            {
                [AuthConstants.OAuth.GrantType] = AuthConstants.OAuth.GrantTypeAuthorizationCode,
                [AuthConstants.OAuth.Code] = code,
                [AuthConstants.OAuth.RedirectUri] = _keycloak.RedirectUri,
                [AuthConstants.OAuth.CodeVerifier] = codeVerifier,
            }, ct);
        }

        public async Task<KeycloakTokenResponse> RefreshAsync(string refreshToken, CancellationToken ct)
        {
            return await PostToTokenEndpointAsync(new Dictionary<string, string>
            {
                [AuthConstants.OAuth.GrantType] = AuthConstants.OAuth.GrantTypeRefreshToken,
                [AuthConstants.OAuth.RefreshToken] = refreshToken,
            }, ct);
        }

        public async Task<string> BuildEndSessionUrlAsync(string? idToken, CancellationToken ct)
        {
            var configuration = await _discovery.GetConfigurationAsync(ct);

            var parameters = new Dictionary<string, string?>
            {
                [AuthConstants.OAuth.ClientId] = _keycloak.Client,
            };

            // Only sent when configured. Keycloak validates this against the client's Valid
            // Post Logout Redirect URIs and aborts the whole end-session request with
            // "invalid redirect URI" if it does not match — which leaves the SSO session
            // alive and the user silently signed back in. Leaving it blank is the safe
            // default until the URI is registered: Keycloak then shows its own
            // logged-out page, and the session still ends.
            if (!string.IsNullOrWhiteSpace(_keycloak.PostLogoutRedirectUri))
                parameters[AuthConstants.OAuth.PostLogoutRedirectUri] = _keycloak.PostLogoutRedirectUri;

            // Keycloak only honours post_logout_redirect_uri when it can identify the session,
            // which the id_token_hint gives it.
            if (!string.IsNullOrWhiteSpace(idToken))
                parameters[AuthConstants.OAuth.IdTokenHint] = idToken;

            return QueryHelpers.AddQueryString(configuration.EndSessionEndpoint, parameters);
        }

        /// <summary>
        /// Posts a grant to the token endpoint with client authentication as form fields —
        /// the method proven against this client by the Bruno collection. Do not switch to
        /// client_secret_basic without re-verifying there first.
        /// </summary>
        private async Task<KeycloakTokenResponse> PostToTokenEndpointAsync(
            Dictionary<string, string> form, CancellationToken ct)
        {
            var configuration = await _discovery.GetConfigurationAsync(ct);

            form[AuthConstants.OAuth.ClientId] = _keycloak.Client;
            form[AuthConstants.OAuth.ClientSecret] = _keycloak.Secret;

            using var request = new HttpRequestMessage(HttpMethod.Post, configuration.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(form),
            };

            using var response = await _httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                // Log only Keycloak's error fields. The request body carries the client
                // secret and must never be logged or surfaced in an exception message.
                var error = TryDeserialize<KeycloakErrorResponse>(body);
                _logger.LogError(
                    "Keycloak token request failed with {StatusCode}: {Error} — {ErrorDescription}",
                    (int)response.StatusCode,
                    error?.Error ?? "unknown_error",
                    error?.ErrorDescription ?? "no description");

                throw new ArgumentException(
                    $"Keycloak rejected the token request: {error?.Error ?? "unknown_error"}.");
            }

            return TryDeserialize<KeycloakTokenResponse>(body)
                ?? throw new ArgumentException("Keycloak returned an unreadable token response.");
        }

        private static T? TryDeserialize<T>(string body) where T : class
        {
            try
            {
                return JsonSerializer.Deserialize<T>(body);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// CSPRNG value, base64url-encoded. At <see cref="AuthConstants.SecureRandomByteCount"/>
        /// bytes this yields 43 characters — the RFC 7636 minimum for a code_verifier and
        /// ample entropy for a state value.
        /// </summary>
        private static string CreateSecureRandomString()
        {
            var bytes = RandomNumberGenerator.GetBytes(AuthConstants.SecureRandomByteCount);
            return WebEncoders.Base64UrlEncode(bytes);
        }

        /// <summary>code_challenge = BASE64URL(SHA256(ASCII(code_verifier)))</summary>
        private static string CreateCodeChallenge(string codeVerifier)
        {
            var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
            return WebEncoders.Base64UrlEncode(hash);
        }
    }
}
