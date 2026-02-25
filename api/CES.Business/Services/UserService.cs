using CES.Business.Extensions.Entities;
using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Entities;
using CES.Entities.Infrastructure;
using CES.Entities.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CES.Business.Services
{
    public class UserService:IUserService
    {
        public ICESDataStore _dataStore { get; set; }
        public IPasswordService _passwordService { get; set; }
        public UserService(ICESDataStore dataStore,IPasswordService passwordService) 
        {
            _dataStore = dataStore;
            _passwordService = passwordService;
        }


        //public bool ChangePassword(ChangePasswordModel model,LoggedInUserModel userModel)
        //{
        //    var user = _dataStore.ApplicationUsers.FirstOrDefault(au => au.Id == userModel.UserId && au.IsActive);
        //    if(user == null)
        //    {
        //        return false;
        //    }

        //    user.Password = _passwordService.HashPasword(model.NewPassword);
        //    _dataStore.SaveChanges();
        //    return true;
        //}
    }
}
