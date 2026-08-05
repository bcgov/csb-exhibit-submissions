using CES.Entities;

namespace CES.Business.Interfaces
{
    public interface IUserService
    {
        /// <summary>
        /// Creates or refreshes the CES-local row for a Keycloak-authenticated user, keyed on
        /// the realm subject.
        /// <para>
        /// The row exists so audit records have a stable internal key that survives a name
        /// change in IDIR — it is never a display source, and it never carries a password or
        /// a persisted role. Keycloak stays the single source of truth for authorization.
        /// </para>
        /// </summary>
        Task<ApplicationUser> UpsertFromTokenAsync(
            string keycloakSub,
            string? email,
            string? firstName,
            string? lastName);

        /// <summary>
        /// Creates or refreshes the local row for a dev-bypass mock login, keyed on email
        /// (these accounts have no realm subject).
        /// <para>
        /// Without this the mock path would write null audit ids and local development could
        /// not exercise the user linkage at all.
        /// </para>
        /// </summary>
        Task<ApplicationUser> UpsertMockUserAsync(string email, string firstName, string lastName);

        /// <summary>
        /// Resolves the acting user's <c>ApplicationUser.Id</c> for the audit columns: by realm
        /// subject first, then by email (which is how the mock dev-bypass token identifies
        /// itself). Returns null when no local row matches — the caller records an unattributed
        /// change rather than failing the request.
        /// </summary>
        Task<int?> ResolveUserIdAsync(string? keycloakSub, string? email);
    }
}
