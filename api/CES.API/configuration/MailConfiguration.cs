namespace CES.API
{
    public class MailConfiguration : IMailConfiguration
    {
        public string SmtpServer { get; set; }
        public int SmtpPort { get; set; }
        public string SmtpUsername { get; set; }
        public string SmtpPassword { get; set; }
        public bool UseSSL { get; set; }
        public string DefaultFromName { get; set; }
        public string DefaultFromAddress { get; set; }
        public string AzureMailConnectionString {  get; set; }
    }
}