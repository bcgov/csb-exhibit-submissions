using CES.Entities;
using CES.Entities.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CES.EF
{
    public class CESDataStore:DbContext,ICESDataStore
    {
        public CESDataStore(DbContextOptions<CESDataStore> options) : base(options)
        {            
            var context = this as DbContext;
        }

        public DbSet<UserAuthToken> UserAuthTokens { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.BindRelationships();
        }

        public override int SaveChanges()
        {
            return base.SaveChanges();
        }

        public Task<int> SaveChangesAsync()
        {
            return base.SaveChangesAsync();
        }
    }
}
