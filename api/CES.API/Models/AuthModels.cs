using CES.API.Authentication;

namespace CES.API.Models
{
    /// <summary>What <c>AuthCallback.vue</c> posts after Keycloak redirects the browser back.</summary>
    public class AuthCallbackRequest
    {
        public string Code { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
    }

    /// <summary>
    /// The callback response. Deliberately carries no refresh token — that stays in the
    /// encrypted HttpOnly cookie and must never reach JavaScript.
    /// </summary>
    public class AuthCallbackResponse
    {
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>Access-token lifetime in seconds; the SPA schedules its renewal from this.</summary>
        public int ExpiresIn { get; set; }

        /// <summary>Validated at /api/auth/login and carried in the encrypted login cookie.</summary>
        public string ReturnUrl { get; set; } = AuthConstants.DefaultReturnUrl;
    }
}
