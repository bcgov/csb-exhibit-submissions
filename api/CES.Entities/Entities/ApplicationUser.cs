using CES.Entities.Infrastructure;

namespace CES.Entities
{
    public class ApplicationUser:BaseEntity
    {
        public string FirstName { get;set; }
        public string LastName { get;set; }
        public string Email {  get;set; } 
        public string Password {  get;set; }
        public bool IsActive {  get;set; }

        public string GetFullName()
        {
            return $"{FirstName} {LastName}";
        }
    }
}
