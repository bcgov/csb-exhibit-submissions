using CES.Business.Interfaces;
using CES.Entities;
using CES.Entities.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CES.Business.Services
{
    public class UserService:IUserService
    {
        public ICESDataStore _dataStore { get; set; }
        public UserService(ICESDataStore dataStore)
        {
            _dataStore = dataStore;
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

            var isNew = user is null;
            if (user is null)
            {
                user = new ApplicationUser
                {
                    KeycloakSub = keycloakSub,
                    IsActive = true,
                };

                _dataStore.ApplicationUser.Add(user);
            }

            return await SaveIdentityAsync(user, isNew, email, firstName, lastName);
        }

        /// <inheritdoc />
        public async Task<ApplicationUser> UpsertMockUserAsync(string email, string firstName, string lastName)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("An email is required to upsert a mock user.");

            var user = await FindByEmailAsync(email);

            var isNew = user is null;
            if (user is null)
            {
                // KeycloakSub stays null — the unique index is filtered to non-null values
                // precisely so these rows do not collide with each other.
                user = new ApplicationUser { IsActive = true };
                _dataStore.ApplicationUser.Add(user);
            }

            return await SaveIdentityAsync(user, isNew, email, firstName, lastName);
        }

        /// <inheritdoc />
        public async Task<int?> ResolveUserIdAsync(string? keycloakSub, string? email)
        {
            if (!string.IsNullOrWhiteSpace(keycloakSub))
            {
                var bySub = await _dataStore.ApplicationUser
                    .Where(candidate => candidate.KeycloakSub == keycloakSub)
                    .Select(candidate => (int?)candidate.Id)
                    .FirstOrDefaultAsync();

                if (bySub.HasValue)
                    return bySub;
            }

            // The mock dev-bypass token carries the email as its subject, so fall back to
            // whichever of the two actually looks like one.
            var candidateEmail = !string.IsNullOrWhiteSpace(email) ? email : keycloakSub;
            if (string.IsNullOrWhiteSpace(candidateEmail))
                return null;

            return (await FindByEmailAsync(candidateEmail))?.Id;
        }

        private Task<ApplicationUser?> FindByEmailAsync(string email)
        {
            var normalised = email.Trim().ToLower();
            return _dataStore.ApplicationUser
                .FirstOrDefaultAsync(candidate => candidate.Email.ToLower() == normalised);
        }

        /// <summary>
        /// Refreshes the identity columns from the provider and persists, stamping the row's own
        /// audit columns with the user it describes — a login is self-created and self-updated,
        /// and that keeps the FK populated rather than leaving an unattributable null.
        /// </summary>
        /// <param name="isNew">
        /// Supplied by the caller rather than inferred from <c>user.Id</c>: EF assigns a
        /// temporary key value on <c>Add</c>, so an unsaved row does not have an Id of zero.
        /// </param>
        private async Task<ApplicationUser> SaveIdentityAsync(
            ApplicationUser user, bool isNew, string? email, string? firstName, string? lastName)
        {
            // Refreshed from the provider on every login so the row cannot drift from IDIR.
            user.Email = email ?? string.Empty;
            user.FirstName = firstName ?? string.Empty;
            user.LastName = lastName ?? string.Empty;

            if (!isNew)
                user.SetUpdateBy(user.Id);

            await _dataStore.SaveChangesAsync();

            if (isNew)
            {
                // Id is only known after the insert, so the self-reference is a second write.
                user.CreatedByUserId = user.Id;
                await _dataStore.SaveChangesAsync();
            }

            return user;
        }
    }
}
