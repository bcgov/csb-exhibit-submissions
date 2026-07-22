using CES.Business.Constants;
using CES.Business.Interfaces;
using CES.Entities;
using CES.Entities.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CES.Business.Services
{
    public class UserService:IUserService
    {
        public ICESDataStore _dataStore { get; set; }
        public IPasswordService _passwordService { get; set; }
        public UserService(ICESDataStore dataStore,IPasswordService passwordService)
        {
            _dataStore = dataStore;
            _passwordService = passwordService;
        }

        /// <inheritdoc />
        public async Task<ApplicationUser> UpsertFromTokenAsync(
            string keycloakSub,
            string? email,
            string? firstName,
            string? lastName)
        {
            if (string.IsNullOrWhiteSpace(keycloakSub))
                throw new ArgumentException("A Keycloak subject is required to upsert a user.");

            var user = await _dataStore.ApplicationUser
                .FirstOrDefaultAsync(candidate => candidate.KeycloakSub == keycloakSub);

            if (user is null)
            {
                user = new ApplicationUser
                {
                    KeycloakSub = keycloakSub,
                    // Password stays unset: Keycloak owns authentication, and a
                    // Keycloak-provisioned row must never carry a credential.
                    Password = string.Empty,
                    IsActive = true,
                    CreatedBy = UserConstants.KeycloakProvisionedBy,
                };

                _dataStore.ApplicationUser.Add(user);
            }
            else
            {
                user.SetUpdateBy(UserConstants.KeycloakProvisionedBy);
            }

            // Refreshed from the token on every login so the row cannot drift from IDIR.
            user.Email = email ?? string.Empty;
            user.FirstName = firstName ?? string.Empty;
            user.LastName = lastName ?? string.Empty;

            await _dataStore.SaveChangesAsync();

            return user;
        }

        //public bool ChangePassword(ChangePasswordModel model,LoggedInUserModel userModel)
        //{
        //    var user = _dataStore.ApplicationUsers.FirstOrDefault(au => au.Id == userModel.UserId && au.IsActive);
        //    if(user == null)
        //    {
        //        return false;
        //    }

        //    user.Password = _passwordService.HashPasword(model.NewPassword);
        //    _dataStore.SaveChanges();
        //    return true;
        //}
    }
}
