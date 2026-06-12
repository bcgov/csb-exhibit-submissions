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
            modelBuilder.Entity<Submission>()
                .HasMany(s => s.Tickets)
                .WithOne(t => t.Submission)
                .HasForeignKey(t => t.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
