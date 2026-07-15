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

        public string GetFullName()
        {
            return $"{FirstName} {LastName}";
        }
    }
}
