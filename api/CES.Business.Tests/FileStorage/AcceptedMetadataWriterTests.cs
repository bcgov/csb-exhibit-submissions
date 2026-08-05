using System.Text.Json;
using CES.Business.Constants;
using CES.Business.FileStorage;
using CES.Entities;
using CES.Entities.Enums;
using FluentAssertions;

namespace CES.Business.Tests.FileStorage;

public class AcceptedMetadataWriterTests
{
    private const int OfficerUserId = 1;
    private const int AdminUserId = 2;
    private const string OfficerEmail = "officer@test.ca";
    private const string AdminEmail = "admin@test.ca";

    // The writer resolves actors through the navigation, which is what the service layer
    // Includes before calling in; these tests attach it directly.
    private static ApplicationUser BuildUser(int id, string email) =>
        new() { Id = id, Email = email, FirstName = "Test", LastName = "User", IsActive = true };

    private static Submission BuildSubmission()
    {
        var submission = new Submission
        {
            ShortDate = "20260101",
            LocationId = "LOC001",
            RoomCode = "ROOM1",
            Status = SubmissionStatus.Accepted,
            Tickets = new List<SubmissionTicket>
            {
                new() { AppearanceId = "APP001", FileNumberText = "FILE001", AccusedName = "Smith, John" },
                new() { AppearanceId = "APP002", FileNumberText = "FILE002", AccusedName = "Smith, John" },
            },
        };
        submission.Files.Add(new StoredFiles
        {
            Id = Guid.NewGuid(),
            OriginalFileName = "bodycam.mp4",
            ContentType = "video/mp4",
            FileSize = 2048,
            IsAccepted = true,
            AcceptedAtUTC = DateTime.UtcNow,
            CanonicalPath = "loc001/room1/20260101/1/guid.mp4",
            Sha256 = "ABCD1234",
            MarkedValue = "A",
        });
        return submission;
    }

    [Fact]
    public void BuildMetadata_IncludesSchemaVersionHashAndAcceptedExhibits()
    {
        var submission = BuildSubmission();

        var metadata = AcceptedMetadataWriter.BuildMetadata(submission, Array.Empty<SubmissionAuditLog>());

        metadata.SchemaVersion.Should().Be(AcceptedStorageConstants.MetadataSchemaVersion);
        metadata.HashAlgorithm.Should().Be(AcceptedStorageConstants.HashAlgorithm);
        metadata.Exhibits.Should().HaveCount(1);
        metadata.Exhibits[0].Sha256.Should().Be("ABCD1234");
        metadata.Exhibits[0].CanonicalPath.Should().Be("loc001/room1/20260101/1/guid.mp4");
    }

    // CES-42: descriptions are exported as their full append-only history, oldest first.
    [Fact]
    public void BuildMetadata_ExportsFullDescriptionHistory_InOrder()
    {
        var submission = BuildSubmission();
        var file = submission.Files.First();
        var firstAdded = DateTime.UtcNow.AddMinutes(-10);
        file.Descriptions.Add(new ExhibitDescription
        {
            DescriptionText = "an addendum",
            CreatedByUserId = AdminUserId,
            CreatedByUser = BuildUser(AdminUserId, AdminEmail),
            CreatedAtUTC = firstAdded.AddMinutes(5),
        });
        file.Descriptions.Add(new ExhibitDescription
        {
            DescriptionText = "first description",
            CreatedByUserId = OfficerUserId,
            CreatedByUser = BuildUser(OfficerUserId, OfficerEmail),
            CreatedAtUTC = firstAdded,
        });

        var metadata = AcceptedMetadataWriter.BuildMetadata(submission, Array.Empty<SubmissionAuditLog>());

        var descriptions = metadata.Exhibits[0].Descriptions;
        descriptions.Select(d => d.Text).Should().ContainInOrder("first description", "an addendum");
        // The sidecar records the actor's email, not the ApplicationUser id it is linked by.
        descriptions[0].By.Should().Be(OfficerEmail);
        descriptions[0].AtUTC.Should().Be(firstAdded);
    }

    [Fact]
    public void BuildMetadata_RecordsAllAssociatedTickets_ForDeDup()
    {
        var submission = BuildSubmission();

        var metadata = AcceptedMetadataWriter.BuildMetadata(submission, Array.Empty<SubmissionAuditLog>());

        // One file, many tickets — the full ticket mapping is preserved for traceability.
        metadata.Exhibits[0].AssociatedTickets.Should().BeEquivalentTo("FILE001", "FILE002");
        metadata.Tickets.Should().HaveCount(2);
    }

    [Fact]
    public void BuildMetadata_ExcludesUnacceptedAndDeletedFiles()
    {
        var submission = BuildSubmission();
        submission.Files.Add(new StoredFiles { Id = Guid.NewGuid(), OriginalFileName = "pending.mp4", ContentType = "video/mp4", IsAccepted = false });
        submission.Files.Add(new StoredFiles { Id = Guid.NewGuid(), OriginalFileName = "removed.mp4", ContentType = "video/mp4", IsAccepted = true, IsDeleted = true });

        var metadata = AcceptedMetadataWriter.BuildMetadata(submission, Array.Empty<SubmissionAuditLog>());

        metadata.Exhibits.Should().HaveCount(1);
        metadata.Exhibits[0].OriginalFileName.Should().Be("bodycam.mp4");
    }

    [Fact]
    public void BuildMetadata_MapsAuditLogsToRevisions_InChronologicalOrder()
    {
        var submission = BuildSubmission();
        var fileId = submission.Files[0].Id;
        var logs = new List<SubmissionAuditLog>
        {
            new() { FileId = fileId, FieldName = "EnteredValue", OldValue = "A", NewValue = "B", ChangedByUserId = OfficerUserId, ChangedByUser = BuildUser(OfficerUserId, OfficerEmail), ChangedAtUTC = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc) },
            new() { FileId = fileId, FieldName = "MarkedValue", OldValue = null, NewValue = "A", ChangedByUserId = OfficerUserId, ChangedByUser = BuildUser(OfficerUserId, OfficerEmail), ChangedAtUTC = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
        };

        var metadata = AcceptedMetadataWriter.BuildMetadata(submission, logs);

        metadata.Revisions.Should().HaveCount(2);
        metadata.Revisions[0].Change.Should().Contain("MarkedValue");
        metadata.Revisions[1].Change.Should().Contain("EnteredValue");
        // Revision history is attributed by email so the file reads on its own.
        metadata.Revisions.Should().OnlyContain(r => r.By == OfficerEmail);
    }

    [Fact]
    public async Task WriteAsync_ProducesSingleReadableMetadataFile_AndNoTemp()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"meta-test-{Guid.NewGuid()}");
        try
        {
            var submission = BuildSubmission();
            var metadata = AcceptedMetadataWriter.BuildMetadata(submission, Array.Empty<SubmissionAuditLog>());

            await AcceptedMetadataWriter.WriteAsync(folder, metadata);

            var files = Directory.GetFiles(folder);
            files.Should().ContainSingle();
            Path.GetFileName(files[0]).Should().Be(AcceptedStorageConstants.MetadataFileName);
            Directory.GetFiles(folder, $"*{AcceptedStorageConstants.TempSuffix}").Should().BeEmpty();

            // Re-parses cleanly (valid JSON, camelCase properties).
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(files[0]));
            doc.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(AcceptedStorageConstants.MetadataSchemaVersion);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAsync_OverwritesExistingMetadata_Atomically()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"meta-test-{Guid.NewGuid()}");
        try
        {
            var submission = BuildSubmission();
            await AcceptedMetadataWriter.WriteAsync(folder, AcceptedMetadataWriter.BuildMetadata(submission, Array.Empty<SubmissionAuditLog>()));

            // Second write (e.g. a metadata edit) replaces in place, still one file.
            submission.Files[0].MarkedValue = "B";
            await AcceptedMetadataWriter.WriteAsync(folder, AcceptedMetadataWriter.BuildMetadata(submission, Array.Empty<SubmissionAuditLog>()));

            Directory.GetFiles(folder).Should().ContainSingle();
            var text = await File.ReadAllTextAsync(Path.Combine(folder, AcceptedStorageConstants.MetadataFileName));
            text.Should().Contain("\"markedValue\": \"B\"");
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAsync_EscapesReadably_NoUnicodeEscapes()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"meta-test-{Guid.NewGuid()}");
        try
        {
            var submission = BuildSubmission();
            var fileId = submission.Files[0].Id;
            var logs = new List<SubmissionAuditLog>
            {
                new() { FileId = fileId, FieldName = "MarkedValue", OldValue = null, NewValue = "A", ChangedByUserId = OfficerUserId, ChangedByUser = BuildUser(OfficerUserId, OfficerEmail), ChangedAtUTC = DateTime.UtcNow },
            };

            await AcceptedMetadataWriter.WriteAsync(folder, AcceptedMetadataWriter.BuildMetadata(submission, logs));

            var text = await File.ReadAllTextAsync(Path.Combine(folder, AcceptedStorageConstants.MetadataFileName));
            // The relaxed encoder keeps the change string human-readable.
            text.Should().NotContain("\\u");
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }
}
