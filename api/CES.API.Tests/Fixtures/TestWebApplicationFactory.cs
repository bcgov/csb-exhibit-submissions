using CES.Business.Interfaces;
using CES.EF;
using CES.Entities.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CES.API.Tests.Fixtures;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    // Shared root ensures all DbContext instances within this factory
    // instance see the same data, even with ServiceProviderCaching disabled.
    private readonly InMemoryDatabaseRoot _dbRoot = new();
    private readonly string _dbName = $"TestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CORS:AllowedOrigins:0"] = "http://localhost",
                ["UserAuth:Key"] = "thisismysecretkeytherearemanylikeitbutthisoneismine",
                ["UserAuth:Issuer"] = "CES-PoC-Local",
                ["UserAuth:Audience"] = "EvidenceSubmission-User",
                ["UserAuth:DurationMinutes"] = "90",
                ["MailConfiguration:SmtpServer"] = "localhost",
                ["MailConfiguration:UseSSL"] = "false",
                ["MailConfiguration:SmtpPort"] = "25",
                ["MailConfiguration:DefaultFromName"] = "Test",
                ["MailConfiguration:DefaultFromAddress"] = "test@test.com",
                ["FileStorage:LocalPath"] = Path.GetTempPath(),
                ["FileStorage:AcceptedPath"] = Path.GetTempPath(),
                ["FileStorage:Provider"] = "Local",
                ["FileStorage:MaxFileSize"] = "104857600",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove all EF-related registrations to avoid dual-provider conflict
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<CESDataStore>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(IDbContextOptionsConfiguration<CESDataStore>) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>)))
                .ToList();

            foreach (var descriptor in toRemove)
                services.Remove(descriptor);

            var dbName = _dbName;
            var dbRoot = _dbRoot;
            services.AddDbContext<CESDataStore>(options =>
                options
                    .UseInMemoryDatabase(dbName, dbRoot)
                    .EnableServiceProviderCaching(false));

            services.AddScoped<ICESDataStore>(sp => sp.GetRequiredService<CESDataStore>());

            services.AddScoped<IFileStorage, InMemoryFileStorage>();
        });
    }
}
