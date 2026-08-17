using CES.Business.Constants;

namespace CES.API.Authentication
{
    /// <summary>
    /// The fixed accounts behind the mock dev-bypass login, which is only reachable while
    /// <c>Keycloak:Enabled</c> is false — the Keycloak scheme rejects locally-signed tokens,
    /// so these grant nothing in a deployed environment.
    /// <para>
    /// Names are carried alongside the credentials because a login provisions an
    /// ApplicationUser row, and the audit trail resolves its display value from it.
    /// </para>
    /// </summary>
    public static class DevBypassUsers
    {
        public record DevBypassUser(
            string Email, string Password, string Role, string FirstName, string LastName);

        /// <summary>Keyed on the lowercased email the client signs in with.</summary>
        public static readonly IReadOnlyDictionary<string, DevBypassUser> All =
            new Dictionary<string, DevBypassUser>(StringComparer.Ordinal)
            {
                ["admin@gov.bc.ca"] =
                    new("admin@gov.bc.ca", "pass123", RoleConstants.Admin, "Dev", "Admin"),
                ["officer@gov.bc.ca"] =
                    new("officer@gov.bc.ca", "pass123", RoleConstants.User, "Dev", "Officer"),
                ["clerk@gov.bc.ca"] =
                    new("clerk@gov.bc.ca", "pass123", RoleConstants.Clerk, "Dev", "Clerk"),
            };
    }
}
