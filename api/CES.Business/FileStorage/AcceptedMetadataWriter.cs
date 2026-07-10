using System.Text.Encodings.Web;
using System.Text.Json;
using CES.Business.Constants;
using CES.Business.Extensions.Entities;
using CES.Business.Models;
using CES.Entities;

namespace CES.Business.FileStorage
{
    // Produces/refreshes the per-submission metadata.json sidecar, derived from the
    // DB (the source of truth). One file per submission folder — no pointer copies
    // with the submission-leaf layout (CES-39, Phase 3).
    public static class AcceptedMetadataWriter
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            // The sidecar is a human-readable audit artifact — use the relaxed encoder
            // so '>' and non-ASCII stay legible rather than being escaped to \uXXXX.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        // Builds the metadata model from DB truth. Includes accepted, non-deleted
        // exhibits; revisions are mapped from the submission's audit-log rows.
        public static AcceptedMetadata BuildMetadata(Submission submission, IEnumerable<SubmissionAuditLog> auditLogs)
        {
            var acceptedFiles = submission.Files
                .Where(f => !f.IsDeleted && f.IsAccepted)
                .ToList();

            var associatedTickets = submission.Tickets
                .Select(t => t.FileNumberText)
                .Where(fn => !string.IsNullOrEmpty(fn))
                .ToList();

            var metadata = new AcceptedMetadata
            {
                SchemaVersion = AcceptedStorageConstants.MetadataSchemaVersion,
                SubmissionId = submission.Id,
                Status = submission.Status.ToString(),
                AcceptedAtUTC = acceptedFiles.Where(f => f.AcceptedAtUTC.HasValue)
                    .Select(f => f.AcceptedAtUTC)
                    .DefaultIfEmpty(null)
                    .Min(),
                LastUpdatedUTC = acceptedFiles.Select(f => f.UpdatedDateUTC ?? f.AcceptedAtUTC)
                    .Where(d => d.HasValue)
                    .DefaultIfEmpty(null)
                    .Max(),
                HashAlgorithm = AcceptedStorageConstants.HashAlgorithm,
                Tickets = submission.Tickets.Select(t => new AcceptedMetadataTicket
                {
                    AppearanceId = t.AppearanceId,
                    FileNumberText = t.FileNumberText,
                    AccusedName = t.AccusedName,
                }).ToList(),
                Exhibits = acceptedFiles.Select(f => new AcceptedMetadataExhibit
                {
                    ExhibitId = f.Id,
                    OriginalFileName = f.OriginalFileName,
                    CanonicalPath = f.CanonicalPath,
                    ContentType = f.ContentType,
                    FileSize = f.FileSize,
                    Sha256 = f.Sha256,
                    IsAccepted = f.IsAccepted,
                    AcceptedAtUTC = f.AcceptedAtUTC,
                    MarkedValue = f.MarkedValue,
                    MarkedAt = f.MarkedAt,
                    EnteredValue = f.EnteredValue,
                    EnteredAt = f.EnteredAt,
                    Description = f.Description,
                    EvidenceSourceType = f.EvidenceSourceType,
                    AssociatedTickets = associatedTickets.ToList(),
                }).ToList(),
                Revisions = auditLogs
                    .OrderBy(l => l.ChangedAtUTC)
                    .Select(l => new AcceptedMetadataRevision
                    {
                        AtUTC = l.ChangedAtUTC,
                        By = l.ChangedBy,
                        Change = $"{l.FieldName} {l.OldValue ?? "(none)"} : {l.NewValue ?? "(none)"} on exhibitId {l.FileId}",
                    }).ToList(),
            };

            return metadata;
        }

        // Serializes the metadata and writes it atomically (temp + rename) into the
        // submission folder. Leaves no .tmp behind on success.
        public static async Task WriteAsync(string submissionFolderFullPath, AcceptedMetadata metadata)
        {
            Directory.CreateDirectory(submissionFolderFullPath);

            var finalPath = Path.Combine(submissionFolderFullPath, AcceptedStorageConstants.MetadataFileName);
            var tempPath = finalPath + AcceptedStorageConstants.TempSuffix;

            await using (var tempStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(tempStream, metadata, SerializerOptions);
                await tempStream.FlushAsync();
            }

            File.Move(tempPath, finalPath, overwrite: true);
        }
    }
}
