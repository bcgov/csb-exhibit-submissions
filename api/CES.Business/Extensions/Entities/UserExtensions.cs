using CES.Business.Models;
using CES.Entities;
using CES.Entities.Enums;
using CES.Entities.Infrastructure;
using CES.Entities.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace CES.Business.Extensions.Entities
{
    public static class UserExtensions
    {
        public static CESLoginReturnModel ToLoginModel(this ApplicationUser user, IConfiguration _configuration,bool rememberMe=false)
        {
            var authDomain = _configuration.GetSection("UserAuth").GetSection("Domain").GetValue<string>("PortalSite");
            var authKey = _configuration.GetSection("UserAuth").GetValue<string>("Key") ?? throw new InvalidOperationException("Invalid UserAuth.Key");
            var authDuration = _configuration.GetSection("UserAuth").GetValue<float>("DurationMinutes");

            var secretkey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(authKey)); // NOTE: SAME KEY AS USED IN Program.cs FILE
            var credentials = new SigningCredentials(secretkey, SecurityAlgorithms.HmacSha256);

            var claims = new[] // NOTE: could also use List<Claim> here
            {
                new Claim(ClaimTypes.Name, user.Id.ToString()),
                new Claim(ClaimTypes.UserData,user.EmailId.ToString()),
                new Claim(ClaimTypes.Role,user.LastActiveRole.ToString())

            };
            var expiry = SystemDate.UtcNow().AddMinutes(authDuration);
            if(rememberMe)
            {
                expiry = SystemDate.UtcNow().AddDays(5);  
            }

            var token = new JwtSecurityToken(issuer: authDomain, audience: authDomain, claims: claims, expires: expiry, signingCredentials: credentials);

            return new CESLoginReturnModel
            {
                AuthToken = new JwtSecurityTokenHandler().WriteToken(token),
                UserName = user.GetFullName(),
                UserId = user.Id,
                TokenExpiryUTC = expiry.ToString(),
                ChangePassword = false,
                LastActiveRole = user.LastActiveRole
            };

        }

        public static LoggedInUserModel ToLoggedInUserModel(this ClaimsPrincipal user)
        {
            var email = user.Claims.FirstOrDefault(cl => cl.Type == ClaimTypes.UserData)?.Value.ToString() ?? "";
            var role = user.Claims.FirstOrDefault(cl => cl.Type == ClaimTypes.Role)?.Value.ToString() ?? "";

            return new LoggedInUserModel
            {
                UserId = int.Parse(user.Identity.Name),
                Email = email,
                LastActiveRole = role
            };
        }

        public static ApplicationUser ToEntity(this UserModel model)
        {
            var user = new ApplicationUser();
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.EmailId = model.EmailId;

            return user;
        }
        
        public static ApplicationUser ToEntity(this UserModel model, ApplicationUser user)
        {
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.EmailId = model.EmailId;
            

            return user;
        }

        public static UserModel ToDetailModel(this ApplicationUser user)
        {
            return new UserModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                EmailId = user.EmailId,
                RoleNames = string.Join(",", user.ApplicationUserRoles.Select(aur => aur.Role))
            };
        }
    }
}
