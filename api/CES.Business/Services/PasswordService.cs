using CES.Business.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CES.Business.Services
{
    public class PasswordService:IPasswordService
    {
        const int keySize = 12;
        public string HashPassword(string password)
        {
            string salt = BCrypt.Net.BCrypt.GenerateSalt(keySize);
            var hash = BCrypt.Net.BCrypt.HashPassword(password, salt);
            return hash;
        }

        public bool VerifyPassword(string password, string hashedPassword)
        {
            bool verification = BCrypt.Net.BCrypt.Verify(password, hashedPassword);
            return verification;
        }
    }
}
