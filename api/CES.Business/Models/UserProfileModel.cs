namespace CES.Business.Models
{
    /// <summary>
    /// The signed-in user's own CES-local row. Identity fields are echoed for display only —
    /// they are refreshed from the token on every login, so the token remains authoritative.
    /// </summary>
    public class UserProfileModel
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        /// <summary>Null until the officer supplies it; never set for Admin/Clerk users.</summary>
        public string? OfficerNumber { get; set; }
    }

    /// <summary>Body of <c>PUT /api/users/me/officer-number</c>.</summary>
    public class OfficerNumberUpdateModel
    {
        /// <summary>
        /// Nullable so an omitted value fails the service's own validation with a readable
        /// message rather than a model-binding error.
        /// </summary>
        public string? OfficerNumber { get; set; }
    }
}
