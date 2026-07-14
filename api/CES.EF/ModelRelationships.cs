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

            modelBuilder.Entity<Submission>()
                .HasMany(s => s.Files)
                .WithOne(f => f.Submission)
                .HasForeignKey(f => f.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SubmissionAuditLog>()
                .HasOne(a => a.Submission)
                .WithMany()
                .HasForeignKey(a => a.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SubmissionAuditLog>()
                .HasOne(a => a.File)
                .WithMany()
                .HasForeignKey(a => a.FileId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ExhibitNote>()
                .HasOne(n => n.File)
                .WithMany()
                .HasForeignKey(n => n.FileId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StoredFiles>()
                .HasMany(f => f.Descriptions)
                .WithOne(d => d.File)
                .HasForeignKey(d => d.FileId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ExhibitDescription>()
                .HasIndex(d => new { d.FileId, d.CreatedAtUTC });
        }
    }
}
