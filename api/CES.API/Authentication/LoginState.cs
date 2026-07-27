using System.Text.Json.Serialization;

namespace CES.API.Authentication
{
    /// <summary>
    /// Payload of the short-lived <c>ces.login</c> cookie. Encrypted with Data Protection,
    /// so the browser cannot read or tamper with any of it — which is why the validated
    /// <see cref="ReturnUrl"/> can ride here rather than through the browser's URL.
    /// </summary>
    public class LoginState
    {
        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        [JsonPropertyName("codeVerifier")]
        public string CodeVerifier { get; set; } = string.Empty;

        /// <summary>Already validated by <see cref="ReturnUrlValidator"/> before being written here.</summary>
        [JsonPropertyName("returnUrl")]
        public string ReturnUrl { get; set; } = AuthConstants.DefaultReturnUrl;

        [JsonPropertyName("issuedAtUtc")]
        public DateTimeOffset IssuedAtUtc { get; set; }

        /// <summary>
        /// True once the login round-trip has outlived <see cref="AuthConstants.LoginStateLifetimeMinutes"/>.
        /// Checked server-side rather than trusting the cookie's own MaxAge.
        /// </summary>
        public bool IsExpired(DateTimeOffset utcNow) =>
            utcNow > IssuedAtUtc.AddMinutes(AuthConstants.LoginStateLifetimeMinutes);
    }

    /// <summary>
    /// Payload of the <c>ces.session</c> cookie: the credentials that must never reach
    /// browser-readable storage. Encrypted with Data Protection and scoped to /api/auth.
    /// </summary>
    public class SessionState
    {
        [JsonPropertyName("refreshToken")]
        public string RefreshToken { get; set; } = string.Empty;

        /// <summary>Retained only to supply <c>id_token_hint</c> on RP-initiated logout.</summary>
        [JsonPropertyName("idToken")]
        public string? IdToken { get; set; }

        [JsonPropertyName("issuedAtUtc")]
        public DateTimeOffset IssuedAtUtc { get; set; }
    }
}
