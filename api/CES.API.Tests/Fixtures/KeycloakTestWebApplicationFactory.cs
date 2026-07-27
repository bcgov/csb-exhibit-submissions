using CES.API.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CES.API.Tests.Fixtures;

/// <summary>
/// The base factory with Keycloak:Enabled flipped on and the token service faked, so the
/// AuthController endpoints are reachable without a realm or a client secret.
/// </summary>
public class KeycloakTestWebApplicationFactory : TestWebApplicationFactory
{
    /// <summary>Placeholder values — no real Authority or client id belongs in a tracked file.</summary>
    public const string RedirectUri = "http://localhost:9080/auth/callback";

    public FakeKeycloakTokenService TokenService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // UseSetting, not ConfigureAppConfiguration: Program.cs reads the Keycloak section
        // eagerly while building the host, which is before an added configuration source
        // would be visible.
        builder.UseSetting("Keycloak:Enabled", "true");
        builder.UseSetting("Keycloak:Authority", "https://keycloak.test/realms/ces");
        builder.UseSetting("Keycloak:Client", "ces-test-client");
        builder.UseSetting("Keycloak:RedirectUri", RedirectUri);
        builder.UseSetting("Keycloak:PostLogoutRedirectUri", "http://localhost:9080/");
        // Keeps the Data Protection key ring out of the repo during test runs.
        builder.UseSetting("DataProtection:KeyPath", Path.Combine(Path.GetTempPath(), "ces-test-keys"));

        // Registered last so it wins over the typed HttpClient registration in Program.cs.
        builder.ConfigureServices(services =>
            services.AddSingleton<IKeycloakTokenService>(TokenService));
    }
}
