namespace CES.API
{
    /// <summary>
    /// Binds the "Keycloak" configuration section.
    /// <para>
    /// <see cref="Secret"/> is supplied by environment variable / secret store only —
    /// it must never appear in a tracked appsettings file, a log statement, a response
    /// body, or an exception message.
    /// </para>
    /// </summary>
    public class KeycloakConfiguration
    {
        /// <summary>When false the API runs the mock dev-bypass login instead.</summary>
        public bool Enabled { get; set; }

        /// <summary>Realm base URL. All endpoints are resolved from its discovery document.</summary>
        public string Authority { get; set; } = string.Empty;

        /// <summary>OIDC client id.</summary>
        public string Client { get; set; } = string.Empty;

        /// <summary>OIDC client secret. Environment/secret-store only.</summary>
        public string Secret { get; set; } = string.Empty;

        /// <summary>
        /// The SPA callback route. Must byte-match a Valid Redirect URI registered on the
        /// client, and must be sent identically on the authorize request and the token
        /// exchange — Keycloak returns invalid_grant otherwise.
        /// </summary>
        public string RedirectUri { get; set; } = string.Empty;

        /// <summary>Where Keycloak returns the browser after an RP-initiated logout.</summary>
        public string PostLogoutRedirectUri { get; set; } = string.Empty;
    }
}
