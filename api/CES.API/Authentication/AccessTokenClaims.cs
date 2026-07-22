using Microsoft.IdentityModel.JsonWebTokens;

namespace CES.API.Authentication
{
    /// <summary>
    /// The identity claims CES persists against a Keycloak login.
    /// </summary>
    public record AccessTokenClaims(string Subject, string? Email, string? FirstName, string? LastName);

    /// <summary>
    /// Reads identity claims straight off an access token without re-validating the
    /// signature. That is safe only here: the token was just returned by Keycloak over TLS
    /// on a client-authenticated exchange, so it has not been through a browser. Tokens
    /// arriving on the wire from a client are validated by the JwtBearer handler instead.
    /// </summary>
    public static class AccessTokenReader
    {
        /// <summary>
        /// Returns null when the token cannot be parsed or carries no subject — the caller
        /// treats that as "no local user row", not as a failed login.
        /// </summary>
        public static AccessTokenClaims? Read(string? accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                return null;

            try
            {
                var token = new JsonWebToken(accessToken);

                var subject = ValueOrNull(token, AuthConstants.SubjectClaim);
                if (string.IsNullOrWhiteSpace(subject))
                    return null;

                return new AccessTokenClaims(
                    subject,
                    ValueOrNull(token, AuthConstants.EmailClaim),
                    ValueOrNull(token, AuthConstants.GivenNameClaim),
                    ValueOrNull(token, AuthConstants.FamilyNameClaim));
            }
            catch (ArgumentException)
            {
                // Not a readable JWT.
                return null;
            }
        }

        private static string? ValueOrNull(JsonWebToken token, string claimType) =>
            token.TryGetClaim(claimType, out var claim) ? claim.Value : null;
    }
}
