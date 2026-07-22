namespace CES.Business.Constants
{
    public static class UserConstants
    {
        /// <summary>
        /// Audit stamp on rows provisioned by the Keycloak login, distinguishing them from
        /// the seeded/mock rows that carry the BaseEntity default.
        /// </summary>
        public const string KeycloakProvisionedBy = "Keycloak";
    }
}
