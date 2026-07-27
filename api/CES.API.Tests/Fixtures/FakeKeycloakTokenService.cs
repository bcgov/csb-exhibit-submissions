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

    /// <summary>Distinct from <see cref="RefreshToken"/> so a test can prove rotation was written back.</summary>
    public const string RotatedRefreshToken = "rotated-refresh-token-value";
    public const string IdToken = "fake-id-token";
    public const string EndSessionUrl = "https://keycloak.test/realms/ces/protocol/openid-connect/logout";
    public const int ExpiresIn = 300;

    /// <summary>When set, ExchangeCodeAsync throws as the real service does on a Keycloak error.</summary>
    public bool RejectExchange { get; set; }

    /// <summary>When set, RefreshAsync throws (expired / already-rotated / revoked grant).</summary>
    public bool RejectRefresh { get; set; }

    /// <summary>
    /// When set, RefreshAsync returns no id_token — Keycloak's refresh grant is not required
    /// to include one, and the controller must carry the existing id_token forward.
    /// </summary>
    public bool RefreshReturnsNoIdToken { get; set; }

    /// <summary>When set, BuildEndSessionUrlAsync throws; logout must still clear the cookie.</summary>
    public bool RejectEndSession { get; set; }

    /// <summary>Overrides the access token the exchange returns, e.g. a real JWT for the upsert path.</summary>
    public string? AccessTokenOverride { get; set; }

    /// <summary>The verifier the controller pulled out of the login cookie and passed back in.</summary>
    public string? ObservedCodeVerifier { get; private set; }

    /// <summary>The refresh token the controller read back out of the session cookie.</summary>
    public string? ObservedRefreshToken { get; private set; }

    /// <summary>The id_token the controller handed to logout — used to prove it survived a refresh.</summary>
    public string? ObservedLogoutIdToken { get; private set; }

    public int ExchangeCallCount { get; private set; }
    public int RefreshCallCount { get; private set; }
    public int EndSessionCallCount { get; private set; }

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
            AccessToken = AccessTokenOverride ?? AccessToken,
            RefreshToken = RefreshToken,
            IdToken = IdToken,
            ExpiresIn = ExpiresIn,
        });
    }

    public Task<KeycloakTokenResponse> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        RefreshCallCount++;
        ObservedRefreshToken = refreshToken;

        if (RejectRefresh)
            throw new ArgumentException("Keycloak rejected the token request: invalid_grant.");

        return Task.FromResult(new KeycloakTokenResponse
        {
            AccessToken = AccessTokenOverride ?? AccessToken,
            // Rotated: distinct value, so a follow-up refresh proves the cookie was rewritten.
            RefreshToken = RotatedRefreshToken,
            IdToken = RefreshReturnsNoIdToken ? null : IdToken,
            ExpiresIn = ExpiresIn,
        });
    }

    public Task<string> BuildEndSessionUrlAsync(string? idToken, CancellationToken ct)
    {
        EndSessionCallCount++;
        ObservedLogoutIdToken = idToken;

        if (RejectEndSession)
            throw new InvalidOperationException("End-session URL could not be built.");

        return Task.FromResult(EndSessionUrl);
    }
}
