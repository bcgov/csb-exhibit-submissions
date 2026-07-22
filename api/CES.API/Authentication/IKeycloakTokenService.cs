namespace CES.API.Authentication
{
    /// <summary>
    /// The only abstraction in the codebase that touches the Keycloak client secret.
    /// Every method resolves its endpoint from the realm's discovery document.
    /// </summary>
    public interface IKeycloakTokenService
    {
        /// <summary>
        /// Builds the authorize URL and returns it alongside the state/PKCE verifier the
        /// caller must persist in the encrypted <c>ces.login</c> cookie.
        /// </summary>
        /// <param name="returnUrl">Raw value from the query string; sanitized here.</param>
        Task<(string AuthorizeUrl, LoginState State)> BuildAuthorizeRequestAsync(
            string? returnUrl, CancellationToken ct);

        /// <summary>Exchanges an authorization code. Throws <see cref="ArgumentException"/> on a Keycloak error.</summary>
        Task<KeycloakTokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct);

        /// <summary>
        /// Redeems a refresh token. Throws <see cref="ArgumentException"/> when the grant is
        /// rejected (expired / already-rotated / revoked).
        /// </summary>
        Task<KeycloakTokenResponse> RefreshAsync(string refreshToken, CancellationToken ct);

        /// <summary>RP-initiated logout URL, including <c>id_token_hint</c> when one is available.</summary>
        Task<string> BuildEndSessionUrlAsync(string? idToken, CancellationToken ct);
    }
}
