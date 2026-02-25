using CES.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CES.EF
{
    public static class ModelRelationships
    {
        public static void BindRelationships(this ModelBuilder modelBuilder)
        {
            //modelBuilder.Entity<ApplicationUser>()
            //    .HasOne(u => u.Profile)
            //    .WithOne(a => a.User)
            //    .HasForeignKey<Profile>(a => a.UserId);

        }
    }
}
