using CES.Entities.Infrastructure;

namespace CES.Entities
{
    public class ApplicationUser:BaseEntity
    {
        public string FirstName { get;set; } = null!;
        public string LastName { get;set; } = null!;
        public string Email {  get;set; } = null!;
        public bool IsActive {  get;set; }

        /// <summary>
        /// Keycloak subject — stable per user within the realm. Null for legacy/mock
        /// dev-bypass users, which is why this is nullable rather than required.
        /// <para>
        /// Identifier only, never a display source: names and email are always rendered
        /// from the token claims so the record cannot drift from IDIR.
        /// </para>
        /// </summary>
        public string? KeycloakSub { get; set; }

        /// <summary>
        /// The officer's badge/PIN number, supplied by the officer on first use — IDIR does not
        /// expose it as a claim, so it cannot be read off the token like the identity columns.
        /// <para>
        /// Null until they provide it, and only ever set for officer-role users. This is the one
        /// CES-owned, user-editable column on the row: unlike name and email it is never refreshed
        /// from the provider, so a login must not clear it.
        /// </para>
        /// </summary>
        public string? OfficerNumber { get; set; }

        public string GetFullName()
        {
            return $"{FirstName} {LastName}";
        }
    }
}
