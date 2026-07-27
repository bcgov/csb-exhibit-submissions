using CES.Entities.Infrastructure;

namespace CES.Entities
{
    public class ApplicationUser:BaseEntity
    {
        public string FirstName { get;set; } = null!;
        public string LastName { get;set; } = null!;
        public string Email {  get;set; } = null!;
        public string Password {  get;set; } = null!;
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

        public string GetFullName()
        {
            return $"{FirstName} {LastName}";
        }
    }
}
