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

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddScopedServiceCollection(builder.Configuration);

//TODO: File storage service.  Change to adjust how files are saved at upload
builder.Services.AddScoped<IFileStorage, LocalFileStorage>();
builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection("FileStorage"));

// Authentication
builder.Services.AddCESAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

// ** CORS **
var corsPolicyName = "CESCorsPolicy";
var corsSettings = builder.Configuration.GetSection("CORS").Get<CORSSettings>();
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

var mailConfiguration = builder.Configuration.GetSection("MailConfiguration").Get<MailConfiguration>();
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
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
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
    db.Database.Migrate();
    // DataSeedService.SeedDatabase(db);
}
// var recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();
// Jobs.RunAllJobs(recurringJobManager);

app.Run();
