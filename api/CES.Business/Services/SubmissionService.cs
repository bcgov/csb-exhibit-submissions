using CES.Business.Constants;
using CES.Business.Extensions.Entities;
using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Entities;
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

        public async Task<int?> SubmitEvidence(EvidenceSubmissionModel model)
        {
            if (model.Tickets == null || model.Tickets.Count == 0)
                return null;

            // Try to append to an existing Pending submission when a valid, matching id is supplied.
            var entity = await ResolveAppendTargetAsync(model);

            if (entity == null)
            {
                // Create a new submission (first upload, or fallback for an invalid append target).
                entity = model.ToEntity();
                await _datastore.Submissions.AddAsync(entity);

                // Flush now so entity.Id is populated; files are stored under the submission ID.
                await _datastore.SaveChangesAsync();
            }

            // ShortDate is persisted on the submission, so appended files reuse the original folder.
            var storagePath = Path.Combine(entity.LocationId, entity.ShortDate, entity.RoomCode, entity.Id.ToString());

            foreach (var file in model.fileUploads)
            {
                var newFile = await _fileStorage.SaveAsync(file, storagePath);
                entity.Files.Add(newFile);
                await _datastore.StoredFiles.AddAsync(newFile);
            }

            // A new, un-accepted file makes the submission Pending again — an
            // already-Accepted submission that gains a same-session upload reopens
            // until that file is classified/accepted (CES-39, Phase 5).
            entity.RecalculateStatus();

            await _datastore.SaveChangesAsync();
            return entity.Id;
        }

        // Returns the existing submission to append to, or null to create a new one.
        // Appends only when the id refers to a Pending, non-deleted submission whose court
        // context (location + room) matches the current form; otherwise falls back to new.
        private async Task<Submission?> ResolveAppendTargetAsync(EvidenceSubmissionModel model)
        {
            if (!model.SubmissionId.HasValue)
                return null;

            var existing = await _datastore.Submissions
                .Include(s => s.Files)
                .Include(s => s.Tickets)
                .FirstOrDefaultAsync(s => s.Id == model.SubmissionId.Value);

            if (existing == null
                || existing.IsDeleted
                || existing.Status != SubmissionStatus.Pending
                || existing.LocationId != model.LocationId
                || existing.RoomCode != model.RoomCode)
            {
                return null;
            }

            return existing;
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

        public async Task<(bool success, string? error)> RejectSubmissions(SubmissionActionModel model)
        {
            var submission = await _datastore.Submissions
                .Include(s => s.Files)
                .FirstOrDefaultAsync(s => s.Id == model.SubmissionId);

            if (submission == null)
                return (false, "Submission not found.");

            if (submission.Status == SubmissionStatus.Rejected)
                return (false, "Submission is already rejected.");

            var now = SystemDate.UtcNow();
            // Accepted files are never removed (CES-39, Q6) — only delete non-accepted
            // retained files. Accepted bytes stay put even on a whole-submission Reject.
            foreach (var file in submission.Files.Where(f => !f.IsDeleted && !f.IsAccepted))
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

            // An accepted file can never be removed (CES-39, Q6). This preserves
            // current behaviour and sidesteps reference counting.
            if (file.IsAccepted)
                throw new InvalidOperationException("Accepted exhibits cannot be removed.");

            if (file.Submission.Status == SubmissionStatus.Rejected)
                throw new InvalidOperationException("Exhibits cannot be removed from a rejected submission.");

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
