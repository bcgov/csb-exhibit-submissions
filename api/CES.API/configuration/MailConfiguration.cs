namespace CES.API
{
    
    public interface IMailConfiguration
    {
        string SmtpServer { get; }
        int SmtpPort { get; }
        string SmtpUsername { get; set; }
        string SmtpPassword { get; set; }
        bool UseSSL { get; set; }
        string DefaultFromName { get; set; }
        string DefaultFromAddress { get; set; }
        string AzureMailConnectionString {  get; set; }
        
    }
    
    public class MailConfiguration : IMailConfiguration
    {
        public required string SmtpServer { get; set; }
        public int SmtpPort { get; set; }
        public required string SmtpUsername { get; set; }
        public required string SmtpPassword { get; set; }
        public bool UseSSL { get; set; }
        public required string DefaultFromName { get; set; }
        public required string DefaultFromAddress { get; set; }
        public required string AzureMailConnectionString {  get; set; }
    }
}