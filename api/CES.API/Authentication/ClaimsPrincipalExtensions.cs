using System.Security.Claims;
using CES.Business.Interfaces;

namespace CES.API.Authentication
{
    /// <summary>
    /// Reads the identity off an authenticated request and maps it to the CES-local user id
    /// that every audit column points at.
    /// </summary>
    public static class ClaimsPrincipalExtensions
    {
        /// <summary>
        /// The realm subject. The Keycloak handler runs with <c>MapInboundClaims = false</c> so
        /// the claim keeps its wire name; the mock dev-bypass handler leaves the default mapping
        /// on, which rewrites <c>sub</c> to <see cref="ClaimTypes.NameIdentifier"/>. Both are
        /// checked so this works whichever scheme is registered.
        /// </summary>
        public static string? GetSubject(this ClaimsPrincipal principal) =>
            principal.FindFirstValue(AuthConstants.SubjectClaim)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        /// <summary>
        /// The email claim, present on Keycloak tokens. The mock token has none — it carries the
        /// email as its subject instead, which <see cref="IUserService.ResolveUserIdAsync"/>
        /// falls back to.
        /// </summary>
        public static string? GetEmail(this ClaimsPrincipal principal) =>
            principal.FindFirstValue(AuthConstants.EmailClaim)
            ?? principal.FindFirstValue(ClaimTypes.Email);

        /// <summary>
        /// Resolves the acting user's <c>ApplicationUser.Id</c> for the audit columns. Null when
        /// no local row matches: the change is recorded unattributed rather than rejected, since
        /// authorization has already been settled by the token.
        /// </summary>
        public static Task<int?> ResolveUserIdAsync(this ClaimsPrincipal principal, IUserService userService) =>
            userService.ResolveUserIdAsync(principal.GetSubject(), principal.GetEmail());
    }
}
