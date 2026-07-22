namespace CES.API.Authentication
{
    /// <summary>
    /// Named values for the Keycloak authorization-code flow. Per the project rule
    /// against inline magic values, nothing here is repeated as a literal elsewhere.
    /// </summary>
    public static class AuthConstants
    {
        /// <summary>Cookie holding the Data Protection-encrypted refresh token + id_token.</summary>
        public const string SessionCookieName = "ces.session";

        /// <summary>
        /// Cookie holding the encrypted PKCE verifier + state, alive only between the
        /// authorize redirect and the callback.
        /// </summary>
        public const string LoginStateCookieName = "ces.login";

        /// <summary>
        /// Path the auth cookies are scoped to — the browser only ever sends them to the
        /// auth endpoints, never to the submission/file endpoints.
        /// </summary>
        public const string AuthCookiePath = "/api/auth";

        /// <summary>Data Protection purpose strings (distinct purposes cannot be swapped).</summary>
        public const string SessionProtectorPurpose = "CES.Auth.Session.v1";

        /// <inheritdoc cref="SessionProtectorPurpose"/>
        public const string LoginStateProtectorPurpose = "CES.Auth.LoginState.v1";

        /// <summary>
        /// Application name for the Data Protection key ring. Must be an explicit constant
        /// and identical across replicas — ASP.NET otherwise derives it from the content-root
        /// path, and two pods with different paths will not share keys even off one volume.
        /// </summary>
        public const string DataProtectionApplicationName = "CES.API";

        /// <summary>
        /// Fallback key-ring directory (relative to the content root) used when
        /// DataProtection:KeyPath is unset, so a bare `dotnet run` still works.
        /// </summary>
        public const string DefaultDataProtectionKeyDirectory = "keys";

        /// <summary>
        /// Login round-trip budget. An IDIR login that takes longer than this restarts
        /// rather than replaying a stale state/verifier pair.
        /// </summary>
        public const int LoginStateLifetimeMinutes = 10;

        /// <summary>
        /// Matches Keycloak's max SSO session (confirmed with the SSO team, 2026-07).
        /// Past this point the refresh token is dead anyway, so the cookie expires with it
        /// rather than lingering as a token that cannot be redeemed.
        /// Revisit if the SSO team changes the realm's max-session setting.
        /// </summary>
        public const int SessionCookieLifetimeHours = 8;

        /// <summary>Keycloak client roles → CES application roles.</summary>
        public const string KeycloakRoleAdmin = "ces-judicial";

        /// <inheritdoc cref="KeycloakRoleAdmin"/>
        public const string KeycloakRoleUser = "ces-user";

        /// <inheritdoc cref="KeycloakRoleAdmin"/>
        public const string KeycloakRoleClerk = "ces-clerk";

        /// <summary>Claim names.</summary>
        public const string RolesClaim = "roles";

        /// <inheritdoc cref="RolesClaim"/>
        public const string ResourceAccessClaim = "resource_access";

        /// <inheritdoc cref="RolesClaim"/>
        public const string AuthorizedPartyClaim = "azp";

        /// <summary>Identity claims read off the access token to provision the local user row.</summary>
        public const string SubjectClaim = "sub";

        /// <inheritdoc cref="SubjectClaim"/>
        public const string EmailClaim = "email";

        /// <inheritdoc cref="SubjectClaim"/>
        public const string GivenNameClaim = "given_name";

        /// <inheritdoc cref="SubjectClaim"/>
        public const string FamilyNameClaim = "family_name";

        /// <summary>Identity provider hint — government staff only, skips the IDP selector.</summary>
        public const string IdpHint = "idir";

        /// <summary>Scopes requested on the authorize request.</summary>
        public const string Scopes = "openid profile email";

        /// <summary>
        /// Bytes of CSPRNG entropy behind the PKCE verifier and the state value.
        /// 32 bytes base64url-encodes to 43 chars, the minimum RFC 7636 permits for a
        /// code_verifier (43–128).
        /// </summary>
        public const int SecureRandomByteCount = 32;

        /// <summary>Fallback return URL when none was supplied or the supplied one failed validation.</summary>
        public const string DefaultReturnUrl = "/";

        /// <summary>OAuth / OIDC protocol parameter names and values.</summary>
        public static class OAuth
        {
            public const string ClientId = "client_id";
            public const string ClientSecret = "client_secret";
            public const string ResponseType = "response_type";
            public const string ResponseTypeCode = "code";
            public const string RedirectUri = "redirect_uri";
            public const string Scope = "scope";
            public const string State = "state";
            public const string Code = "code";
            public const string CodeVerifier = "code_verifier";
            public const string CodeChallenge = "code_challenge";
            public const string CodeChallengeMethod = "code_challenge_method";
            public const string CodeChallengeMethodS256 = "S256";
            public const string GrantType = "grant_type";
            public const string GrantTypeAuthorizationCode = "authorization_code";
            public const string GrantTypeRefreshToken = "refresh_token";

            /// <summary>Form-field name carrying the refresh token itself (distinct role from the grant type).</summary>
            public const string RefreshToken = "refresh_token";
            public const string IdTokenHint = "id_token_hint";
            public const string PostLogoutRedirectUri = "post_logout_redirect_uri";
            public const string IdpHintParameter = "kc_idp_hint";
        }
    }
}
