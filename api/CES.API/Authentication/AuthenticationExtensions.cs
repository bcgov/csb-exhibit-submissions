using CES.Business.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace CES.API.Authentication
{
    public static class AuthenticationExtensions
    {
        public static IServiceCollection AddCESAuthentication(
            this IServiceCollection services, 
            IConfiguration configuration)
        {
            // temporary service to handle token issuing
            services.AddScoped<ITokenService, LocalTokenService>();
            
            var authSettings = configuration.GetSection("UserAuth").Get<AuthConfiguration>();

            if(authSettings == null)
                return services;


            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = authSettings?.Issuer ?? "CES-Local",
                        ValidAudience = authSettings?.Audience ?? "CES-User",
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(authSettings?.Key!))
                    };
                });

            return services;
        }
    }

}