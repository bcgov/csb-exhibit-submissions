using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CES.Entities.Interfaces
{
    public interface ICESDataStore
    {
        int SaveChanges();
        Task<int> SaveChangesAsync();
        public DbSet<UserAuthToken> UserAuthTokens { get; }
    }
}
