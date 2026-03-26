
using CES.Business.Interfaces;
using CES.Business.Services;
using CES.Business.Extensions;
using JCCommon.Clients.LocationServices;
using JCCommon.Clients.FileServices;
using JCCommon.Framework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace CES.Business
{
    public static class BusinessDependencyExtensions
    {
        public static IServiceCollection AddScopedServiceCollection(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<ILoginService, LoginService>();  
            services.AddScoped<IPasswordService, PasswordService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IDeveloperService, DeveloperService>();
            services.AddScoped<ISubmissionService, SubmissionService>();
            services.AddScoped<IFileService, FileService>();
            
            services.AddHttpClient<LocationServicesClient>(client => { ConfigureHttpClient(client, configuration, "LocationServicesClient"); });
            services.AddHttpClient<FileServicesClient>(client => { ConfigureHttpClient(client, configuration, "FileServicesClient"); });

            return services;
        }


        // the ConfigureHttpClient and associated requirements referenced from Jasper project 
        private static void ConfigureHttpClient(HttpClient client, IConfiguration configuration, string prefix, int timeoutInSecs = 100)
        {

            client.Timeout = TimeSpan.FromSeconds(timeoutInSecs);
            var username = configuration.GetNonEmptyValue($"{prefix}:Username");
            var password = configuration.GetNonEmptyValue($"{prefix}:Password");
            var url = new Uri(configuration.GetNonEmptyValue($"{prefix}:Url").EnsureEndingForwardSlash());
            // Defaults to BC Gov API if any config setting is missing
            client.DefaultRequestHeaders.Authorization = new BasicAuthenticationHeaderValue(
                username,
                password);
            client.BaseAddress = url;

        }

        
        public static string GetNonEmptyValue(this IConfiguration configuration, string key)
        {
            var configurationValue = configuration.GetValue<string>(key);
            return string.IsNullOrEmpty(configurationValue)
                ? throw new ConfigurationException($"Configuration '{key}' is invalid or missing.")
                : configurationValue;
        }
        
        public class ConfigurationException : Exception
        { // copied from Jasper
            public ConfigurationException(string message) : base(message) { }
        }
    }
}
