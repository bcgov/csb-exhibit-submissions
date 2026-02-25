namespace CES.API
{
    public class UserAuthSettings
    {
        public DomainSettings Domain { get; set; }
        public string Key { get; set; }
    }

    public class DomainSettings
    {
        public string PortalSite { get; set; }

    }
}
