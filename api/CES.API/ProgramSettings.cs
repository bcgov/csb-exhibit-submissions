namespace CES.API
{
    public class UserAuthSettings
    {
        public required DomainSettings Domain { get; set; }
        public required string Key { get; set; }
    }

    public class DomainSettings
    {
        public required string PortalSite { get; set; }

    }
}
