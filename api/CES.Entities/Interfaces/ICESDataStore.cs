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
        public DbSet<ApplicationUser> ApplicationUser { get; }
        public DbSet<UserAuthToken> UserAuthTokens { get; }
        public DbSet<Submission> Submissions { get; }
        public DbSet<SubmissionTicket> SubmissionTickets { get; }
        public DbSet<StoredFiles> StoredFiles { get; }
        public DbSet<SubmissionAuditLog> SubmissionAuditLogs { get; }
        public DbSet<ExhibitNote> ExhibitNotes { get; }
    }
}
