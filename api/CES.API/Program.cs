using CES.API;
using CES.Business;
using CES.EF;
using CES.Entities.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using CES.Business.Interfaces;
using CES.API.FileStorage;
using CES.API.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using CES.API.Authentication;
using CES.Business.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
// Aliased because Microsoft.Extensions.Configuration.ConfigurationManager is in the
// implicit usings and would otherwise make the name ambiguous.
using OidcConfigurationManager =
    Microsoft.IdentityModel.Protocols.ConfigurationManager<
        Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration>;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddScopedServiceCollection(builder.Configuration);

//TODO: File storage service.  Change to adjust how files are saved at upload
builder.Services.AddScoped<IFileStorage, LocalFileStorage>();
builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection("FileStorage"));

// Authentication
// Bearer validation stays on the mock JWT until step 5 of the Keycloak spec swaps it
// out behind the same Keycloak:Enabled flag.
builder.Services.AddCESAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

// Keycloak. Registered unconditionally so AuthController can always be constructed;
// every one of its endpoints returns 404 while Keycloak:Enabled is false, and nothing
// here contacts the realm until an endpoint is actually called.
var keycloakConfiguration = builder.Configuration.GetSection("Keycloak").Get<KeycloakConfiguration>()
    ?? new KeycloakConfiguration();
builder.Services.AddSingleton(keycloakConfiguration);

// Discovery document is fetched once and cached here rather than on every login.
builder.Services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(
    new OidcConfigurationManager(
        $"{keycloakConfiguration.Authority.TrimEnd('/')}/.well-known/openid-configuration",
        new OpenIdConnectConfigurationRetriever(),
        new HttpDocumentRetriever { RequireHttps = true }));

builder.Services.AddHttpClient<IKeycloakTokenService, KeycloakTokenService>();

// Data Protection — encrypts the auth cookies (ces.login / ces.session).
// The key path must point at a volume that survives restarts and is shared across
// replicas; otherwise a restarted or second instance cannot decrypt a cookie it did
// not issue, and users are silently bounced to Keycloak mid-session.
var dataProtectionKeyPath = builder.Configuration["DataProtection:KeyPath"];
if (string.IsNullOrWhiteSpace(dataProtectionKeyPath))
{
    dataProtectionKeyPath = Path.Combine(
        builder.Environment.ContentRootPath,
        AuthConstants.DefaultDataProtectionKeyDirectory);
}

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(Directory.CreateDirectory(dataProtectionKeyPath))
    .SetApplicationName(AuthConstants.DataProtectionApplicationName);

// ** CORS **
var corsPolicyName = "CESCorsPolicy";
var corsSettings = builder.Configuration.GetSection("CORS").Get<CORSSettings>()
    ?? throw new InvalidOperationException("Configuration section 'CORS' not found.");
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: corsPolicyName,
        policy =>
        {
            policy
            .WithOrigins(corsSettings.AllowedOrigins)
            .AllowCredentials()
            .SetIsOriginAllowedToAllowWildcardSubdomains()
            .AllowAnyHeader()
            .WithExposedHeaders("X-Pagination")
            .AllowAnyMethod();
        });
});

builder.Services.AddEndpointsApiExplorer();


// builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CSB EXHITBIT SUBMISSIONS", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."

    });
    //c.AddSecurityRequirement(new OpenApiSecurityRequirement
    //        {
    //    {
    //        new OpenApiSecurityScheme
    //        {
    //            Reference = new BaseOpenApiReference
    //            {
    //                Type = ReferenceType.SecurityScheme,
    //                Id = "Bearer"
    //            }
    //        },
    //        new string[] {}
    //    }
    //        });
});

var mailConfiguration = builder.Configuration.GetSection("MailConfiguration").Get<MailConfiguration>()
    ?? throw new InvalidOperationException("Configuration section 'MailConfiguration' not found.");
builder.Services.AddSingleton<IMailConfiguration>(mailConfiguration);

var dataStoreConnectionString = builder.Configuration.GetConnectionString("CESDataStore");
builder.Services.AddDbContext<CESDataStore>(options =>
    options.UseNpgsql(dataStoreConnectionString ?? throw new InvalidOperationException("Connection string 'CESDataStore' not found."))
);

// Add Individual services here, or check for classes implementing a to-be-created interface
builder.Services.AddScoped<ICESDataStore, CESDataStore>();


// var authSettings = builder.Configuration.GetSection("UserAuth").Get<UserAuthSettings>();
// builder.Services.AddAuthentication("Bearer")
//     .AddJwtBearer("Bearer", options =>
//     {
//         options.TokenValidationParameters = new TokenValidationParameters
//         {
//             ValidateAudience = true,
//             ValidAudiences = new[] { authSettings.Domain.PortalSite },
//             //ValidAudience = authSettings.Domain.PortalSite,
//             ValidateIssuer = true,
//             ValidIssuers = new[] { authSettings.Domain.PortalSite },
//             //ValidIssuer = authSettings.Domain.PortalSite,
//             ValidateLifetime = true,
//             ValidateIssuerSigningKey = true,
//             IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(authSettings.Key))
//         };
//     });

// builder.Services.AddAuthorization();
var app = builder.Build();

// Configure the HTTP request pipeline.
var enableSwagger = app.Environment.IsDevelopment() ||
    bool.TryParse(builder.Configuration["EnableSwagger"], out var result) && result;

if (enableSwagger)
{
    var swaggerRoutePrefix = (builder.Configuration["SwaggerRoutePrefix"] ?? "api/swagger").Trim('/');

    app.UseSwagger(c =>
    {
        c.RouteTemplate = $"{swaggerRoutePrefix}/{{documentName}}/swagger.json";
    });

    app.UseSwaggerUI(c =>
    {
        c.RoutePrefix = swaggerRoutePrefix;
        c.SwaggerEndpoint($"/{swaggerRoutePrefix}/v1/swagger.json", "CSB EXHITBIT SUBMISSIONS v1");
    });
}

app.UseHttpsRedirection();
app.UseCors(corsPolicyName);
app.UseAuthentication();
app.UseAuthorization();

// Register custom middleware for error handling
app.UseMiddleware<ApiExceptionMiddleware>();

app.MapControllers();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CESDataStore>();
    if (db.Database.IsRelational())
        db.Database.Migrate();
    else
        db.Database.EnsureCreated();
    // DataSeedService.SeedDatabase(db);
}
// var recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();
// Jobs.RunAllJobs(recurringJobManager);

app.Run();

public partial class Program { }
