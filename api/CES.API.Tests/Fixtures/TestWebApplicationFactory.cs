using CES.API.Authentication;
using CES.Business.Interfaces;
using CES.EF;
using CES.Entities;
using CES.Entities.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

            // Singleton so in-memory file/package state persists across HTTP
            // requests within a single test (e.g. accept then download package).
            services.AddSingleton<IFileStorage, InMemoryFileStorage>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        SeedDevBypassUsers(host);
        return host;
    }

    /// <summary>
    /// Audit columns are FKs to ApplicationUser, so the identities behind
    /// <see cref="JwtTokenHelper"/>'s tokens need rows to resolve to — the same rows a real
    /// mock login would have provisioned before handing out that token.
    /// </summary>
    private static void SeedDevBypassUsers(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CESDataStore>();

        if (db.ApplicationUser.Any())
            return;

        db.ApplicationUser.AddRange(
            DevBypassUsers.All.Values.Select(user => new ApplicationUser
            {
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsActive = true,
            }));
        db.SaveChanges();
    }
}
