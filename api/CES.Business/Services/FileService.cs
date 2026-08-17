using CES.Business.Constants;
using CES.Business.Extensions.Entities;
using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Entities;
using CES.Entities.Infrastructure;
using CES.Entities.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CES.Business.Services
{
    public class FileService : IFileService
    {
        private readonly ICESDataStore _dataStore;
        private readonly IFileStorage _fileStorage;
        private readonly ILogger<FileService> _logger;

        public FileService(ICESDataStore dataStore, IFileStorage fileStorage, ILogger<FileService> logger)
        {
            _dataStore = dataStore;
            _fileStorage = fileStorage;
            _logger = logger;
        }

        public async Task<StoredFiles?> RetrieveFileMetaData(Guid fileId)
        {
            return await _dataStore.StoredFiles.FindAsync(fileId);
        }

        public async Task<(Stream? stream, string? fileName, string? contentType, string? error)> GetExhibitContentAsync(Guid fileId)
        {
            var file = await _dataStore.StoredFiles.FirstOrDefaultAsync(f => f.Id == fileId);

            if (file == null || file.IsDeleted)
                return (null, null, null, "Exhibit not found.");

            try
            {
                // Accepted files come from the canonical store (resolved via the
                // DB-stored path, never a client-supplied one); pending files from
                // the temporary store.
                var stream = file.IsAccepted
                    ? await _fileStorage.GetAcceptedExhibitAsync(file)
                    : await _fileStorage.GetAsync(file);

                return (stream, file.OriginalFileName, file.ContentType, null);
            }
            catch (FileNotFoundException)
            {
                return (null, null, null, "Exhibit file not found.");
            }
        }

        public async Task<SubmissionFile> MarkExhibitAsync(Guid fileId, string markedValue, int? changedByUserId, bool isAdminOverride = false)
        {
            var file = await LoadFileWithSubmissionAsync(fileId)
                ?? throw new KeyNotFoundException($"File {fileId} not found.");

            if (!isAdminOverride && file.EnteredValue != null)
                throw new InvalidOperationException("Entered exhibits cannot be modified.");

            var normalised = markedValue.ToUpperInvariant();
            if (normalised.Length != 1 || normalised[0] < 'A' || normalised[0] > 'Z')
                throw new ArgumentException("Marked value must be a single letter A–Z.");

            var oldValue = file.MarkedValue;
            file.MarkedValue = normalised;
            file.MarkedAt = SystemDate.UtcNow();
            file.SetUpdateBy(changedByUserId);

            _dataStore.SubmissionAuditLogs.Add(new SubmissionAuditLog
            {
                SubmissionId = file.SubmissionId,
                FileId = file.Id,
                FieldName = "MarkedValue",
                OldValue = oldValue,
                NewValue = normalised,
                ChangedByUserId = changedByUserId,
            });

            // Per-file auto-accept on first Marked (Decision #13).
            await FinalizeClassificationAsync(file, autoAccept: true);
            return ToSubmissionFile(file);
        }

        public async Task<SubmissionFile> EnterExhibitAsync(Guid fileId, string enteredValue, int? changedByUserId, bool isAdminOverride = false)
        {
            var file = await LoadFileWithSubmissionAsync(fileId)
                ?? throw new KeyNotFoundException($"File {fileId} not found.");

            if (!int.TryParse(enteredValue, out var numericValue)
                || numericValue < ClassificationConstants.EnteredMin
                || numericValue > ClassificationConstants.EnteredMax)
            {
                throw new ArgumentException($"Entered value must be a number between {ClassificationConstants.EnteredMin} and {ClassificationConstants.EnteredMax}.");
            }

            if (!isAdminOverride && file.EnteredAt.HasValue)
            {
                var secondsSinceEntered = (SystemDate.UtcNow() - file.EnteredAt.Value).TotalSeconds;
                if (secondsSinceEntered > ClassificationConstants.ClassificationEditWindowSeconds)
                    throw new InvalidOperationException("Entered exhibits cannot be modified.");
            }

            var oldValue = file.EnteredValue;
            file.EnteredValue = enteredValue;

            if (!file.EnteredAt.HasValue)
                file.EnteredAt = SystemDate.UtcNow();

            file.SetUpdateBy(changedByUserId);

            _dataStore.SubmissionAuditLogs.Add(new SubmissionAuditLog
            {
                SubmissionId = file.SubmissionId,
                FileId = file.Id,
                FieldName = "EnteredValue",
                OldValue = oldValue,
                NewValue = enteredValue,
                ChangedByUserId = changedByUserId,
            });

            // Per-file auto-accept on first Entered (Decision #13); if already
            // accepted (Marked→Entered) the bytes/sha stay, only metadata refreshes.
            await FinalizeClassificationAsync(file, autoAccept: true);
            return ToSubmissionFile(file);
        }

        // Appends an immutable description entry (CES-42). There is deliberately no
        // update or delete: a correction is a new entry and the earlier ones remain.
        // The entry list is the description's history, so — unlike Marked/Entered/Source
        // — no SubmissionAuditLog row is written.
        public async Task<SubmissionFile> AddExhibitDescriptionAsync(Guid fileId, string descriptionText, int? createdByUserId, bool isAdminOverride = false)
        {
            var file = await LoadFileWithSubmissionAsync(fileId)
                ?? throw new KeyNotFoundException($"File {fileId} not found.");

            if (!isAdminOverride && file.EnteredValue != null)
                throw new InvalidOperationException("Entered exhibits cannot be modified.");

            var normalised = NormaliseDescription(descriptionText);
            if (normalised.Length == 0)
                throw new ArgumentException("Description text is required.");
            if (normalised.Length > ClassificationConstants.DescriptionMaxLength)
                throw new ArgumentException($"Description cannot exceed {ClassificationConstants.DescriptionMaxLength} characters.");

            var entry = new ExhibitDescription
            {
                FileId = file.Id,
                DescriptionText = normalised,
                CreatedByUserId = createdByUserId,
                CreatedAtUTC = SystemDate.UtcNow(),
            };

            // Hydrate the author navigation on the new entry before anything reads it back:
            // the echoed SubmissionFile and the metadata sidecar both resolve the display
            // email through it, and EF will not fix up a navigation for an untracked user.
            if (createdByUserId.HasValue)
                entry.CreatedByUser = await _dataStore.ApplicationUser
                    .FirstOrDefaultAsync(u => u.Id == createdByUserId.Value);

            file.Descriptions.Add(entry);
            file.SetUpdateBy(createdByUserId);

            // Adding a description never triggers acceptance on its own; it only
            // refreshes the sidecar when the file is already accepted (and, per the
            // guard above, not yet Entered).
            await FinalizeClassificationAsync(file, autoAccept: false);
            return ToSubmissionFile(file);
        }

        // Plain text, multiline: line endings are normalised to \n and the entry as a
        // whole is trimmed, but interior whitespace (indentation, blank lines) is kept.
        private static string NormaliseDescription(string? text)
            => (text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n").Trim();

        public async Task<SubmissionFile> UpdateExhibitEvidenceSourceAsync(Guid fileId, string? evidenceSourceType, int? changedByUserId, bool isAdminOverride = false)
        {
            var file = await LoadFileWithSubmissionAsync(fileId)
                ?? throw new KeyNotFoundException($"File {fileId} not found.");

            if (!isAdminOverride && file.EnteredValue != null)
                throw new InvalidOperationException("Entered exhibits cannot be modified.");

            // Empty/whitespace clears the value; otherwise it must be a known source type.
            var normalised = string.IsNullOrWhiteSpace(evidenceSourceType) ? null : evidenceSourceType;
            if (normalised != null && !ClassificationConstants.EvidenceSourceTypes.Contains(normalised))
                throw new ArgumentException($"Evidence source type must be one of: {string.Join(", ", ClassificationConstants.EvidenceSourceTypes)}.");

            var oldValue = file.EvidenceSourceType;
            file.EvidenceSourceType = normalised;
            file.SetUpdateBy(changedByUserId);

            _dataStore.SubmissionAuditLogs.Add(new SubmissionAuditLog
            {
                SubmissionId = file.SubmissionId,
                FileId = file.Id,
                FieldName = "EvidenceSourceType",
                OldValue = oldValue,
                NewValue = normalised,
                ChangedByUserId = changedByUserId,
            });

            // Like a description edit: never triggers acceptance on its own, only
            // refreshes the sidecar when the file is already accepted.
            await FinalizeClassificationAsync(file, autoAccept: false);
            return ToSubmissionFile(file);
        }

        // Loads a file together with its submission's tickets and files so promotion
        // and metadata refresh have the full context they need. Description authors are
        // pulled in as well — the metadata sidecar records them by email.
        private async Task<StoredFiles?> LoadFileWithSubmissionAsync(Guid fileId)
            => await _dataStore.StoredFiles
                .Include(f => f.Descriptions).ThenInclude(d => d.CreatedByUser)
                .Include(f => f.Submission).ThenInclude(s => s.Tickets)
                .Include(f => f.Submission).ThenInclude(s => s.Files)
                    .ThenInclude(sf => sf.Descriptions).ThenInclude(d => d.CreatedByUser)
                .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

        // Persists the DB change (source of truth) first, then promotes bytes /
        // (re)writes the sidecar. If a file operation fails the DB stays authoritative
        // and the sidecar is regenerable on the next edit.
        private async Task FinalizeClassificationAsync(StoredFiles file, bool autoAccept)
        {
            if (autoAccept && !file.IsAccepted)
            {
                var result = await _fileStorage.PromoteToAcceptedAsync(file.Submission, file);
                file.IsAccepted = true;
                file.AcceptedAtUTC = SystemDate.UtcNow();
                file.CanonicalPath = result.CanonicalPath;
                file.Sha256 = result.Sha256;
                file.AcceptedFileName = result.AcceptedFileName;
                file.Submission.RecalculateStatus();
            }

            await _dataStore.SaveChangesAsync();

            if (file.IsAccepted)
            {
                // ChangedByUser is included because the sidecar records the actor's email,
                // not the internal id — the file has to stand on its own outside the DB.
                var auditLogs = await _dataStore.SubmissionAuditLogs
                    .Include(l => l.ChangedByUser)
                    .Where(l => l.SubmissionId == file.SubmissionId)
                    .ToListAsync();
                await _fileStorage.WriteMetadataAsync(file.Submission, auditLogs);

                await CleanupPendingCopyAsync(file);
            }
        }

        // The pending upload is redundant once the exhibit is in the accepted store, so
        // it is removed — but strictly last, after the DB has committed the canonical
        // path and hash the storage layer re-verifies against. Ordering it here means a
        // crash or failure at any earlier point leaves a harmless orphan in uploads
        // rather than an exhibit with nowhere to read its bytes from.
        //
        // Runs on every finalize (not just the promoting one) so an exhibit accepted
        // before this cleanup existed has its original swept up the next time it is
        // touched.
        private async Task CleanupPendingCopyAsync(StoredFiles file)
        {
            try
            {
                var result = await _fileStorage.DeletePendingCopyAsync(file);

                if (result == PendingCleanupResult.VerificationFailed)
                {
                    // The accepted copy could not be confirmed against the hash recorded
                    // at acceptance. The pending original is intentionally kept as the
                    // surviving source of the bytes — this needs a human.
                    _logger.LogError(
                        "Pending copy of exhibit {FileId} (submission {SubmissionId}) retained: the accepted copy at {CanonicalPath} failed verification.",
                        file.Id, file.SubmissionId, file.CanonicalPath);
                }
            }
            catch (Exception ex)
            {
                // Cleanup is best-effort. Acceptance has already succeeded and committed,
                // so a failure to delete must not surface as a failed request.
                _logger.LogError(ex,
                    "Failed to remove the pending copy of exhibit {FileId} (submission {SubmissionId}) after acceptance.",
                    file.Id, file.SubmissionId);
            }
        }

        public async Task<List<ExhibitHistoryEntryModel>> GetExhibitHistoryAsync(Guid fileId)
        {
            var fileExists = await _dataStore.StoredFiles.AnyAsync(f => f.Id == fileId);
            if (!fileExists)
                throw new KeyNotFoundException($"File {fileId} not found.");

            return await _dataStore.SubmissionAuditLogs
                .Where(l => l.FileId == fileId)
                .OrderBy(l => l.ChangedAtUTC)
                .Select(l => new ExhibitHistoryEntryModel
                {
                    FieldName = l.FieldName,
                    OldValue = l.OldValue,
                    NewValue = l.NewValue,
                    ChangedByUserId = l.ChangedByUserId,
                    // Resolved on read from the linked user, so a rename in IDIR is reflected
                    // everywhere rather than leaving a stale copy on the audit row.
                    ChangedBy = l.ChangedByUser != null ? l.ChangedByUser.Email : null,
                    ChangedAtUTC = l.ChangedAtUTC,
                })
                .ToListAsync();
        }

        public async Task<List<ExhibitNoteModel>> GetExhibitNotesAsync(Guid fileId)
        {
            var fileExists = await _dataStore.StoredFiles.AnyAsync(f => f.Id == fileId);
            if (!fileExists)
                throw new KeyNotFoundException($"File {fileId} not found.");

            return await _dataStore.ExhibitNotes
                .Where(n => n.FileId == fileId)
                .OrderBy(n => n.CreatedAtUTC)
                .Select(n => new ExhibitNoteModel
                {
                    Id = n.Id,
                    NoteText = n.NoteText,
                    CreatedByUserId = n.CreatedByUserId,
                    CreatedBy = n.CreatedByUser != null ? n.CreatedByUser.Email : null,
                    CreatedAtUTC = n.CreatedAtUTC,
                })
                .ToListAsync();
        }

        public async Task<ExhibitNoteModel> AddExhibitNoteAsync(Guid fileId, string noteText, int? createdByUserId)
        {
            var fileExists = await _dataStore.StoredFiles.AnyAsync(f => f.Id == fileId);
            if (!fileExists)
                throw new KeyNotFoundException($"File {fileId} not found.");

            var trimmed = (noteText ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                throw new ArgumentException("Note text is required.");
            if (trimmed.Length > ExhibitNoteConstants.NoteMaxLength)
                throw new ArgumentException($"Note cannot exceed {ExhibitNoteConstants.NoteMaxLength} characters.");

            var note = new ExhibitNote
            {
                FileId = fileId,
                NoteText = trimmed,
                CreatedByUserId = createdByUserId,
                CreatedAtUTC = SystemDate.UtcNow(),
            };

            _dataStore.ExhibitNotes.Add(note);
            await _dataStore.SaveChangesAsync();

            return new ExhibitNoteModel
            {
                Id = note.Id,
                NoteText = note.NoteText,
                CreatedByUserId = note.CreatedByUserId,
                CreatedBy = await ResolveEmailAsync(note.CreatedByUserId),
                CreatedAtUTC = note.CreatedAtUTC,
            };
        }

        // The note was just inserted, so its CreatedByUser navigation is not loaded; the
        // echoed response resolves the email directly rather than re-querying the note.
        private async Task<string?> ResolveEmailAsync(int? userId)
        {
            if (!userId.HasValue)
                return null;

            return await _dataStore.ApplicationUser
                .Where(u => u.Id == userId.Value)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();
        }

        private static SubmissionFile ToSubmissionFile(StoredFiles f) => f.ToSubmissionFile();
    }
}
