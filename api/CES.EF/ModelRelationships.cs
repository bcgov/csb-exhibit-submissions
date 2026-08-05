using CES.Entities;
using CES.Entities.Infrastructure;
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

            modelBuilder.BindAuditUserRelationships();
        }

        /// <summary>
        /// Wires every audit column to <see cref="ApplicationUser"/> as a real foreign key, so
        /// "who touched this" is a join rather than a free-text label that drifts.
        /// <para>
        /// All are optional (a system-driven write has no actor) and use
        /// <see cref="DeleteBehavior.Restrict"/>: a user row is retired via <c>IsActive</c>, never
        /// hard-deleted, and an audit trail must never lose its actor to a cascade or a silent null.
        /// </para>
        /// </summary>
        private static void BindAuditUserRelationships(this ModelBuilder modelBuilder)
        {
            // Mapped entities carrying the BaseEntity audit pair. Configured per-type because
            // EF maps the inherited properties separately onto each table.
            // EmailMessage also derives from BaseEntity but has no DbSet and is not reachable
            // from one; configuring it here would pull it into the model and scaffold a table
            // the application does not use.
            modelBuilder.BindBaseEntityAuditUser<ApplicationUser>();
            modelBuilder.BindBaseEntityAuditUser<Submission>();
            modelBuilder.BindBaseEntityAuditUser<UserAuthToken>();

            // StoredFiles declares the same pair itself (Guid key, not a BaseEntity) and
            // exposes navigations so projections can read the actor's email.
            modelBuilder.Entity<StoredFiles>()
                .HasOne(f => f.CreatedByUser)
                .WithMany()
                .HasForeignKey(f => f.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StoredFiles>()
                .HasOne(f => f.UpdatedByUser)
                .WithMany()
                .HasForeignKey(f => f.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SubmissionAuditLog>()
                .HasOne(l => l.ChangedByUser)
                .WithMany()
                .HasForeignKey(l => l.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ExhibitNote>()
                .HasOne(n => n.CreatedByUser)
                .WithMany()
                .HasForeignKey(n => n.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ExhibitDescription>()
                .HasOne(d => d.CreatedByUser)
                .WithMany()
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }

        /// <summary>
        /// Binds the inherited <see cref="BaseEntity"/> audit columns of one entity type. No
        /// navigation is declared: nothing reads these back through the object graph, and on
        /// ApplicationUser a navigation would be a self-reference to no purpose.
        /// </summary>
        private static void BindBaseEntityAuditUser<TEntity>(this ModelBuilder modelBuilder)
            where TEntity : BaseEntity
        {
            modelBuilder.Entity<TEntity>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(entity => entity.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TEntity>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(entity => entity.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
