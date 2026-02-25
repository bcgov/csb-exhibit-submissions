
using CES.Business.Interfaces;
using CES.Business.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace CES.Business
{
    public static class BusinessDependencyExtensions
    {
        public static IServiceCollection AddScopedServiceCollection(this IServiceCollection services)
        {
            services.AddScoped<ILoginService, LoginService>();  
            services.AddScoped<IPasswordService, PasswordService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IDeveloperService, DeveloperService>();

            return services;
        }
    }
}
