using CES.Business.Constants;
using CES.Business.Extensions.Entities;
using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Entities.Enums;
using CES.Entities.Infrastructure;
using CES.Entities.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CES.Business.Services
{
    public class SubmissionService : ISubmissionService
    {
        private readonly ICESDataStore _datastore;
        private readonly IFileStorage _fileStorage;

        public SubmissionService(ICESDataStore dataStore, IFileStorage fileStorage)
        {
            _datastore = dataStore;
            _fileStorage = fileStorage;
        }

        public async Task<bool> SubmitEvidence(EvidenceSubmissionModel model)
        {
            if (model.Tickets == null || model.Tickets.Count == 0)
                return false;

            var entity = model.ToEntity();
            await _datastore.Submissions.AddAsync(entity);

            // Flush now so entity.Id is populated; files are stored under the submission ID.
            await _datastore.SaveChangesAsync();

            var storagePath = Path.Combine(model.LocationId, model.ShortDate, model.RoomCode, entity.Id.ToString());

            foreach (var file in model.fileUploads)
            {
                var newFile = await _fileStorage.SaveAsync(file, storagePath);
                entity.Files.Add(newFile);
                await _datastore.StoredFiles.AddAsync(newFile);
            }

            await _datastore.SaveChangesAsync();
            return true;
        }

        public async Task<SubmissionReviewModel?> RetrieveSubmission(int submissionId)
        {
            var entity = await _datastore.Submissions
                .Include(s => s.Tickets)
                .Include(s => s.Files) // include all files (including Removed) for historical view
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (entity == null)
                return null;

            return entity.ToReviewModel();
        }

        public async Task<PagedResult<SubmissionReviewModel>> RetrieveSubmissionListing(SubmissionListFilter filter)
        {
            var pageSize = Math.Clamp(filter.PageSize, 1, PagingConstants.MaxPageSize);
            if (pageSize == 0) pageSize = PagingConstants.DefaultPageSize;
            var page = Math.Max(1, filter.Page);

            var query = _datastore.Submissions
                .Include(s => s.Tickets)
                .Include(s => s.Files)
                .AsQueryable();

            if (filter.SubmissionDateFrom.HasValue)
                query = query.Where(s => s.UploadDate >= filter.SubmissionDateFrom.Value);

            if (filter.SubmissionDateTo.HasValue)
                query = query.Where(s => s.UploadDate <= filter.SubmissionDateTo.Value);

            if (!string.IsNullOrWhiteSpace(filter.FileNumberText))
            {
                var fnLower = filter.FileNumberText.ToLower();
                query = query.Where(s => s.Tickets.Any(t => t.FileNumberText.ToLower().Contains(fnLower)));
            }

            if (!string.IsNullOrWhiteSpace(filter.AccusedName))
            {
                var nameLower = filter.AccusedName.ToLower();
                query = query.Where(s => s.Tickets.Any(t => t.AccusedName != null && t.AccusedName.ToLower().Contains(nameLower)));
            }

            if (filter.Status.HasValue)
                query = query.Where(s => s.Status == filter.Status.Value);

            var totalCount = await query.CountAsync();

            var submissions = await query
                .OrderByDescending(s => s.UploadDate)
                .ThenBy(s => s.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<SubmissionReviewModel>
            {
                Items = submissions.Select(s => s.ToReviewModel()).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
            };
        }

        public async Task<(bool success, string? error)> AcceptSubmissions(SubmissionActionModel model)
        {
            var submission = await _datastore.Submissions
                .Include(s => s.Files)
                .Include(s => s.Tickets)
                .FirstOrDefaultAsync(s => s.Id == model.SubmissionId);

            if (submission == null)
                return (false, "Submission not found.");

            if (submission.Status != SubmissionStatus.Pending)
                return (false, "Only Pending submissions can be accepted.");

            var unreadyFiles = submission.Files
                .Where(f => !f.IsDeleted && f.EnteredValue == null)
                .Select(f => f.OriginalFileName)
                .ToList();

            if (unreadyFiles.Count > 0)
                return (false, $"All exhibits must be Entered or Removed before accepting. Unready: {string.Join(", ", unreadyFiles)}");

            await _fileStorage.AcceptSubmissionAsync(submission);

            submission.Status = SubmissionStatus.Accepted;
            submission.StatusChangedDateUTC = SystemDate.UtcNow();

            await _datastore.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool success, string? error)> RejectSubmissions(SubmissionActionModel model)
        {
            var submission = await _datastore.Submissions
                .Include(s => s.Files)
                .FirstOrDefaultAsync(s => s.Id == model.SubmissionId);

            if (submission == null)
                return (false, "Submission not found.");

            if (submission.Status != SubmissionStatus.Pending)
                return (false, "Only Pending submissions can be rejected.");

            var now = SystemDate.UtcNow();
            foreach (var file in submission.Files.Where(f => !f.IsDeleted))
            {
                await _fileStorage.DeleteAsync(file);
                file.IsDeleted = true;
                file.DeletedAtUTC = now;
            }

            submission.Status = SubmissionStatus.Rejected;
            submission.StatusChangedDateUTC = now;

            await _datastore.SaveChangesAsync();
            return (true, null);
        }

        public async Task<bool> RemoveFileAsync(Guid fileId)
        {
            var file = await _datastore.StoredFiles
                .Include(f => f.Submission)
                .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

            if (file == null)
                return false;

            if (file.Submission.Status != SubmissionStatus.Pending)
                throw new InvalidOperationException("Exhibits can only be removed from Pending submissions.");

            await _fileStorage.DeleteAsync(file);
            file.IsDeleted = true;
            file.DeletedAtUTC = SystemDate.UtcNow();
            file.SetUpdateBy("Admin");
            await _datastore.SaveChangesAsync();
            return true;
        }

        public async Task<List<PriorSubmissionModel>> GetSubmissionsByFileNumberAsync(string fileNumberText)
        {
            var submissions = await _datastore.Submissions
                .Where(s => !s.IsDeleted && s.Tickets.Any(t => t.FileNumberText == fileNumberText))
                .Include(s => s.Tickets)
                .Include(s => s.Files)
                .OrderByDescending(s => s.UploadDate)
                .ToListAsync();

            return submissions.Select(s =>
            {
                var matchingTicket = s.Tickets.FirstOrDefault(t => t.FileNumberText == fileNumberText);
                return new PriorSubmissionModel
                {
                    SubmissionId = s.Id,
                    SubmissionDate = s.UploadDate,
                    AppearanceDateTime = matchingTicket?.AppearanceDateTime,
                    Location = s.LocationNameText ?? "",
                    Room = s.RoomText ?? "",
                    Files = s.Files.Select(f => new SubmissionFile
                    {
                        Id = f.Id,
                        OriginalFileName = f.OriginalFileName,
                        StoredFileName = f.StoredFileName,
                        ContentType = f.ContentType,
                        FileSize = f.FileSize,
                        StorageProvider = f.StorageProvider,
                        Url = "",
                        Status = f.DeriveStatus(),
                        MarkedValue = f.MarkedValue,
                        MarkedAt = f.MarkedAt,
                        EnteredValue = f.EnteredValue,
                        EnteredAt = f.EnteredAt,
                        Description = f.Description,
                        DeletedAt = f.DeletedAtUTC,
                    }).ToList()
                };
            }).ToList();
        }
    }
}
