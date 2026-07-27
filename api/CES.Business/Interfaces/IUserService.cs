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

        //public bool CreateUser(UserModel model, LoggedInUserModel userModel);
        //public bool ChangePassword(ChangePasswordModel model, LoggedInUserModel userModel);
    }
}
