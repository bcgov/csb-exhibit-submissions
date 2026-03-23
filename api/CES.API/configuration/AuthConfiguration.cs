namespace CES.API
{
    public class Domain
    {
        public string PortalSite { get; } = string.Empty;
    }
    public interface IAuthConfiguration
    {
        public Domain? Domain {get; set;}
        public string Key {get;set;}
        public string Issuer {get; set;}
        public string Audience {get;set;}
        public int DurationMinutes {get;set;}
        public int PasswordChangeRequiredDays {get;set;}
    }

    public class AuthConfiguration : IAuthConfiguration
    {
        public Domain? Domain { get; set; } 
        public string Key { get; set; } = string.Empty;
        public string Issuer {get;set;} = string.Empty;
        public string Audience {get;set;} = string.Empty;
        public int DurationMinutes { get; set; }
        public int PasswordChangeRequiredDays { get; set; }
    }
}