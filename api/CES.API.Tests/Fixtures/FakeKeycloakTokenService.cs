using CES.API.Authentication;

namespace CES.API.Tests.Fixtures;

/// <summary>
/// Stands in for the real token service so AuthController can be exercised end-to-end
/// without a live realm. State and verifier are fixed values so a test can post a
/// matching callback.
/// </summary>
public class FakeKeycloakTokenService : IKeycloakTokenService
{
    public const string State = "fixed-test-state-value";
    public const string CodeVerifier = "fixed-test-code-verifier-value";
    public const string AuthorizeUrl = "https://keycloak.test/realms/ces/protocol/openid-connect/auth?client_id=ces";
    public const string AccessToken = "fake-access-token";
    public const string RefreshToken = "fake-refresh-token-value";
    public const string IdToken = "fake-id-token";
    public const int ExpiresIn = 300;

    /// <summary>When set, ExchangeCodeAsync throws as the real service does on a Keycloak error.</summary>
    public bool RejectExchange { get; set; }

    /// <summary>The verifier the controller pulled out of the login cookie and passed back in.</summary>
    public string? ObservedCodeVerifier { get; private set; }

    public int ExchangeCallCount { get; private set; }

    public Task<(string AuthorizeUrl, LoginState State)> BuildAuthorizeRequestAsync(
        string? returnUrl, CancellationToken ct)
    {
        var state = new LoginState
        {
            State = State,
            CodeVerifier = CodeVerifier,
            ReturnUrl = ReturnUrlValidator.Sanitize(returnUrl),
            IssuedAtUtc = DateTimeOffset.UtcNow,
        };

        return Task.FromResult((AuthorizeUrl, state));
    }

    public Task<KeycloakTokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct)
    {
        ExchangeCallCount++;
        ObservedCodeVerifier = codeVerifier;

        if (RejectExchange)
            throw new ArgumentException("Keycloak rejected the token request: invalid_grant.");

        return Task.FromResult(new KeycloakTokenResponse
        {
            AccessToken = AccessToken,
            RefreshToken = RefreshToken,
            IdToken = IdToken,
            ExpiresIn = ExpiresIn,
        });
    }

    public Task<KeycloakTokenResponse> RefreshAsync(string refreshToken, CancellationToken ct) =>
        Task.FromResult(new KeycloakTokenResponse
        {
            AccessToken = AccessToken,
            RefreshToken = RefreshToken,
            IdToken = IdToken,
            ExpiresIn = ExpiresIn,
        });

    public Task<string> BuildEndSessionUrlAsync(string? idToken, CancellationToken ct) =>
        Task.FromResult("https://keycloak.test/realms/ces/protocol/openid-connect/logout");
}
