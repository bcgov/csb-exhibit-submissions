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
}