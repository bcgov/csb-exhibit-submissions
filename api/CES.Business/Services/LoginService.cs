using CES.Business.Extensions.Entities;
using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Entities;
using CES.Entities.Interfaces;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CES.Business.Services
{
    public class LoginService:ILoginService
    {
        public IConfiguration _configuration {  get; set; }
        public ICESDataStore _dataStore { get; set; }
        public IPasswordService _passwordService { get; set; }
        public LoginService(IConfiguration configuration, ICESDataStore dataStore, IPasswordService passwordService)
        {
            _configuration = configuration;
            _dataStore = dataStore;
            _passwordService = passwordService;
        }

        public CESLoginReturnModel LoginUser(CESLoginModel model)
        {
            return new CESLoginReturnModel();
        }
    }
}
    