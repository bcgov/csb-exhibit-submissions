using CES.Business.Constants;
using CES.Business.Extensions.Entities;
using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Business.Services;
using CES.EF;
using CES.Entities;
using CES.Entities.Enums;
using CES.Entities.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CES.Business.Tests.Services;

public class FileServiceTests : IDisposable
{
    private readonly CESDataStore _db;
    private readonly Mock<IFileStorage> _fileStorageMock;
    private readonly FileService _service;

    public FileServiceTests()
    {
        var options = new DbContextOptionsBuilder<CESDataStore>()
            .UseInMemoryDatabase($"FileServiceTests_{Guid.NewGuid()}")
            .Options;
        _db = new CESDataStore(options);

        _fileStorageMock = new Mock<IFileStorage>();
        // Auto-accept promotion returns a canonical result derived from the ids.
        _fileStorageMock
            .Setup(s => s.PromoteToAcceptedAsync(It.IsAny<Submission>(), It.IsAny<StoredFiles>()))
            .ReturnsAsync((Submission sub, StoredFiles f) => new AcceptedFileResult
            {
                CanonicalPath = $"loc001/room1/20260101/{sub.Id}/{f.Id}{Path.GetExtension(f.OriginalFileName)}",
                AcceptedFileName = $"{f.Id}{Path.GetExtension(f.OriginalFileName)}",
                Sha256 = "DEADBEEF",
            });
        _fileStorageMock
            .Setup(s => s.WriteMetadataAsync(It.IsAny<Submission>(), It.IsAny<IReadOnlyList<SubmissionAuditLog>>()))
            .Returns(Task.CompletedTask);

        _service = new FileService(_db, _fileStorageMock.Object);
    }

    public void Dispose() => _db.Dispose();

    private StoredFiles SeedFile(
        Guid? id = null,
        string? markedValue = null,
        string? enteredValue = null,
        DateTime? enteredAt = null,
        bool isAccepted = false)
    {
        // Classification now auto-accepts, which needs the parent submission (with
        // its tickets and files) loaded — so every seeded file hangs off a submission.
        var submission = new Submission
        {
            ShortDate = "20260101",
            LocationId = "LOC001",
            RoomCode = "ROOM1",
            Tickets = new List<SubmissionTicket>
            {
                new() { AppearanceId = "APP001", FileNumberText = "FILE001", AccusedName = "Smith, John" },
            },
        };
        var file = new StoredFiles
        {
            Id = id ?? Guid.NewGuid(),
            OriginalFileName = "exhibit.mp4",
            StoredFileName = "stored.mp4",
            StoredPath = "loc/2026-01-01/room/1",
            ContentType = "video/mp4",
            FileSize = 1024,
            StorageProvider = "Local",
            MarkedValue = markedValue,
            EnteredValue = enteredValue,
            EnteredAt = enteredAt,
            IsAccepted = isAccepted,
            CanonicalPath = isAccepted ? "loc001/room1/20260101/1/canonical.mp4" : null,
        };
        submission.Files.Add(file);
        _db.Submissions.Add(submission);
        _db.SaveChanges();
        return file;
    }

    // ── RetrieveFileMetaData ──────────────────────────────────────────────

    [Fact]
    public async Task RetrieveFileMetaData_ReturnsEntity_WhenExists()
    {
        var id = Guid.NewGuid();
        _db.StoredFiles.Add(new StoredFiles
        {
            Id = id,
            OriginalFileName = "test.mp4",
            StoredFileName = $"{id}.mp4",
            StoredPath = "location/2026-01-01/room1/filenum",
            ContentType = "video/mp4",
            FileSize = 1024,
            StorageProvider = "Local"
        });
        await _db.SaveChangesAsync();

        var result = await _service.RetrieveFileMetaData(id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
        result.OriginalFileName.Should().Be("test.mp4");
    }

    [Fact]
    public async Task RetrieveFileMetaData_ReturnsNull_WhenNotFound()
    {
        var result = await _service.RetrieveFileMetaData(Guid.NewGuid());

        result.Should().BeNull();
    }

    // ── MarkExhibitAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task MarkExhibit_PersistsValueTimestampAndAudit()
    {
        var file = SeedFile();
        var before = DateTime.UtcNow;

        var result = await _service.MarkExhibitAsync(file.Id, "B", "officer@test.ca");

        var dbFile = _db.StoredFiles.Find(file.Id)!;
        dbFile.MarkedValue.Should().Be("B");
        dbFile.MarkedAt.Should().NotBeNull().And.BeOnOrAfter(before);

        var log = _db.SubmissionAuditLogs.Single(l => l.FileId == file.Id && l.FieldName == "MarkedValue");
        log.OldValue.Should().BeNull();
        log.NewValue.Should().Be("B");
        log.ChangedBy.Should().Be("officer@test.ca");

        result.MarkedValue.Should().Be("B");
        result.Status.Should().Be("Marked");
    }

    [Fact]
    public async Task MarkExhibit_NormalisesLetterToUppercase()
    {
        var file = SeedFile();

        var result = await _service.MarkExhibitAsync(file.Id, "c", "officer@test.ca");

        result.MarkedValue.Should().Be("C");
        _db.StoredFiles.Find(file.Id)!.MarkedValue.Should().Be("C");
    }

    [Fact]
    public async Task MarkExhibit_Rejects_WhenAlreadyEntered()
    {
        var file = SeedFile(enteredValue: "5", enteredAt: DateTime.UtcNow);

        var act = async () => await _service.MarkExhibitAsync(file.Id, "A", "officer@test.ca");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Entered*");
    }

    [Fact]
    public async Task MarkExhibit_Rejects_WhenValueNotSingleLetter()
    {
        var file = SeedFile();

        var act = async () => await _service.MarkExhibitAsync(file.Id, "AB", "officer@test.ca");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task MarkExhibit_Rejects_WhenFileNotFound()
    {
        var act = async () => await _service.MarkExhibitAsync(Guid.NewGuid(), "A", "officer@test.ca");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── EnterExhibitAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task EnterExhibit_PersistsValueTimestampAndAudit()
    {
        var file = SeedFile();
        var before = DateTime.UtcNow;

        var result = await _service.EnterExhibitAsync(file.Id, "7", "officer@test.ca");

        var dbFile = _db.StoredFiles.Find(file.Id)!;
        dbFile.EnteredValue.Should().Be("7");
        dbFile.EnteredAt.Should().NotBeNull().And.BeOnOrAfter(before);

        var log = _db.SubmissionAuditLogs.Single(l => l.FileId == file.Id && l.FieldName == "EnteredValue");
        log.OldValue.Should().BeNull();
        log.NewValue.Should().Be("7");
        log.ChangedBy.Should().Be("officer@test.ca");

        result.EnteredValue.Should().Be("7");
        result.Status.Should().Be("Entered");
    }

    [Fact]
    public async Task EnterExhibit_SucceedsOnMarkedFile()
    {
        var file = SeedFile(markedValue: "A");

        var result = await _service.EnterExhibitAsync(file.Id, "3", "officer@test.ca");

        result.Status.Should().Be("Entered");
        result.MarkedValue.Should().Be("A");
        result.EnteredValue.Should().Be("3");
    }

    [Fact]
    public async Task EnterExhibit_AllowsOverwrite_WithinWindow()
    {
        var file = SeedFile();
        await _service.EnterExhibitAsync(file.Id, "3", "officer@test.ca");
        var firstEnteredAt = _db.StoredFiles.Find(file.Id)!.EnteredAt;

        var result = await _service.EnterExhibitAsync(file.Id, "4", "officer@test.ca");

        result.EnteredValue.Should().Be("4");
        // EnteredAt must not advance on correction
        _db.StoredFiles.Find(file.Id)!.EnteredAt.Should().Be(firstEnteredAt);
    }

    [Fact]
    public async Task EnterExhibit_Rejects_WhenWindowExpired()
    {
        // Seed with EnteredAt already outside the edit window
        var file = SeedFile(
            enteredValue: "3",
            enteredAt: DateTime.UtcNow - TimeSpan.FromSeconds(11));

        var act = async () => await _service.EnterExhibitAsync(file.Id, "4", "officer@test.ca");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be modified*");
    }

    [Fact]
    public async Task MarkExhibit_Rejects_WhenEntered_EvenWithinWindow()
    {
        var file = SeedFile();
        await _service.EnterExhibitAsync(file.Id, "3", "officer@test.ca");

        // Immediately try to go backwards; Entered blocks all Marked writes
        var act = async () => await _service.MarkExhibitAsync(file.Id, "A", "officer@test.ca");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Entered*");
    }

    [Fact]
    public async Task EnterExhibit_Rejects_WhenValueOutOfRange()
    {
        var file = SeedFile();

        await Assert.ThrowsAsync<ArgumentException>(() => _service.EnterExhibitAsync(file.Id, "0", "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.EnterExhibitAsync(file.Id, "51", "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.EnterExhibitAsync(file.Id, "abc", "x"));
    }

    // ── AddExhibitDescriptionAsync (CES-42, append-only) ──────────────────

    [Fact]
    public async Task AddDescription_PersistsEntry_WithoutAuditRow()
    {
        var file = SeedFile();

        var result = await _service.AddExhibitDescriptionAsync(file.Id, "key piece of evidence", "officer@test.ca");

        var entry = _db.ExhibitDescriptions.Single(d => d.FileId == file.Id);
        entry.DescriptionText.Should().Be("key piece of evidence");
        entry.CreatedBy.Should().Be("officer@test.ca");

        result.Descriptions.Should().ContainSingle()
            .Which.DescriptionText.Should().Be("key piece of evidence");

        // The entry list is the description's history — it is not an audited field.
        _db.SubmissionAuditLogs.Should().NotContain(l => l.FieldName == "Description");
    }

    [Fact]
    public async Task AddDescription_Appends_AndKeepsEarlierEntries()
    {
        var file = SeedFile();

        await _service.AddExhibitDescriptionAsync(file.Id, "first", "officer@test.ca");
        var result = await _service.AddExhibitDescriptionAsync(file.Id, "an addendum", "officer@test.ca");

        _db.ExhibitDescriptions.Count(d => d.FileId == file.Id).Should().Be(2);
        result.Descriptions.Select(d => d.DescriptionText)
            .Should().ContainInOrder("first", "an addendum");
    }

    [Fact]
    public async Task AddDescription_NormalisesLineEndings_AndPreservesInteriorWhitespace()
    {
        var file = SeedFile();

        var result = await _service.AddExhibitDescriptionAsync(file.Id, "  line one\r\n\r\n    indented\t line  ", "officer@test.ca");

        result.Descriptions.Single().DescriptionText.Should().Be("line one\n\n    indented\t line");
    }

    [Fact]
    public async Task AddDescription_Rejects_WhenEntered()
    {
        var file = SeedFile(enteredValue: "5", enteredAt: DateTime.UtcNow);

        var act = async () => await _service.AddExhibitDescriptionAsync(file.Id, "notes", "officer@test.ca");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Entered*");
    }

    [Fact]
    public async Task AddDescription_Rejects_WhenWhitespaceOnly()
    {
        var file = SeedFile();

        var act = async () => await _service.AddExhibitDescriptionAsync(file.Id, "   \n  ", "officer@test.ca");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*required*");
    }

    [Fact]
    public async Task AddDescription_Rejects_WhenOverMaxLength()
    {
        var file = SeedFile();
        var tooLong = new string('x', ClassificationConstants.DescriptionMaxLength + 1);

        var act = async () => await _service.AddExhibitDescriptionAsync(file.Id, tooLong, "officer@test.ca");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage($"*{ClassificationConstants.DescriptionMaxLength}*");
    }

    [Fact]
    public async Task AddDescription_Throws_WhenFileNotFound()
    {
        var act = async () => await _service.AddExhibitDescriptionAsync(Guid.NewGuid(), "text", "officer@test.ca");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── UpdateExhibitEvidenceSourceAsync ──────────────────────────────────

    [Fact]
    public async Task UpdateEvidenceSource_PersistsAndAudits()
    {
        var file = SeedFile();

        var result = await _service.UpdateExhibitEvidenceSourceAsync(file.Id, "BodyCam", "officer@test.ca");

        var dbFile = _db.StoredFiles.Find(file.Id)!;
        dbFile.EvidenceSourceType.Should().Be("BodyCam");

        var log = _db.SubmissionAuditLogs.Single(l => l.FileId == file.Id && l.FieldName == "EvidenceSourceType");
        log.NewValue.Should().Be("BodyCam");
        log.ChangedBy.Should().Be("officer@test.ca");

        result.EvidenceSourceType.Should().Be("BodyCam");
    }

    [Fact]
    public async Task UpdateEvidenceSource_Rejects_WhenEntered()
    {
        var file = SeedFile(enteredValue: "5", enteredAt: DateTime.UtcNow);

        var act = async () => await _service.UpdateExhibitEvidenceSourceAsync(file.Id, "DashCam", "officer@test.ca");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Entered*");
    }

    [Fact]
    public async Task UpdateEvidenceSource_AdminOverride_Succeeds_WhenEntered()
    {
        var file = SeedFile(enteredValue: "5", enteredAt: DateTime.UtcNow);

        var result = await _service.UpdateExhibitEvidenceSourceAsync(file.Id, "DashCam", "admin@test.ca", isAdminOverride: true);

        result.EvidenceSourceType.Should().Be("DashCam");
        _db.StoredFiles.Find(file.Id)!.EvidenceSourceType.Should().Be("DashCam");
    }

    [Fact]
    public async Task UpdateEvidenceSource_Rejects_InvalidValue()
    {
        var file = SeedFile();

        var act = async () => await _service.UpdateExhibitEvidenceSourceAsync(file.Id, "Drone", "officer@test.ca");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*BodyCam*");
    }

    [Fact]
    public async Task UpdateEvidenceSource_AllowsEmpty_ToUnset()
    {
        var file = SeedFile();
        await _service.UpdateExhibitEvidenceSourceAsync(file.Id, "BodyCam", "officer@test.ca");

        var result = await _service.UpdateExhibitEvidenceSourceAsync(file.Id, "", "officer@test.ca");

        result.EvidenceSourceType.Should().BeNull();
        _db.StoredFiles.Find(file.Id)!.EvidenceSourceType.Should().BeNull();
        _db.SubmissionAuditLogs.Count(l => l.FileId == file.Id && l.FieldName == "EvidenceSourceType")
            .Should().Be(2);
    }

    [Fact]
    public async Task UpdateEvidenceSource_Succeeds_ForMarkedFile()
    {
        var file = SeedFile(markedValue: "A");

        var result = await _service.UpdateExhibitEvidenceSourceAsync(file.Id, "Other", "officer@test.ca");

        result.EvidenceSourceType.Should().Be("Other");
    }

    [Fact]
    public async Task UpdateEvidenceSource_Throws_WhenFileNotFound()
    {
        var act = async () => await _service.UpdateExhibitEvidenceSourceAsync(Guid.NewGuid(), "BodyCam", "officer@test.ca");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── DeriveStatus extension ────────────────────────────────────────────

    [Fact]
    public void StatusProjection_DerivesUnclassifiedMarkedEntered()
    {
        var unclassified = new StoredFiles { Id = Guid.NewGuid(), OriginalFileName = "f", StoredFileName = "f", StoredPath = "p", ContentType = "video/mp4", StorageProvider = "Local" };
        var marked = new StoredFiles { Id = Guid.NewGuid(), OriginalFileName = "f", StoredFileName = "f", StoredPath = "p", ContentType = "video/mp4", StorageProvider = "Local", MarkedValue = "A" };
        var entered = new StoredFiles { Id = Guid.NewGuid(), OriginalFileName = "f", StoredFileName = "f", StoredPath = "p", ContentType = "video/mp4", StorageProvider = "Local", EnteredValue = "3" };
        var removed = new StoredFiles { Id = Guid.NewGuid(), OriginalFileName = "f", StoredFileName = "f", StoredPath = "p", ContentType = "video/mp4", StorageProvider = "Local", IsDeleted = true };

        unclassified.DeriveStatus().Should().Be("Unclassified");
        marked.DeriveStatus().Should().Be("Marked");
        entered.DeriveStatus().Should().Be("Entered");
        removed.DeriveStatus().Should().Be("Removed");
    }

    // ── GetExhibitHistoryAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetExhibitHistory_ReturnsEntriesInChronologicalOrder()
    {
        var file = SeedFile();
        await _service.MarkExhibitAsync(file.Id, "A", "officer@test.ca");
        await _service.UpdateExhibitEvidenceSourceAsync(file.Id, "BodyCam", "officer@test.ca");
        await _service.EnterExhibitAsync(file.Id, "5", "admin@test.ca", isAdminOverride: true);

        var history = await _service.GetExhibitHistoryAsync(file.Id);

        history.Should().HaveCount(3);
        history.Select(h => h.FieldName).Should().ContainInOrder("MarkedValue", "EvidenceSourceType", "EnteredValue");
        history[0].NewValue.Should().Be("A");
        history[0].ChangedBy.Should().Be("officer@test.ca");
        history.Should().BeInAscendingOrder(h => h.ChangedAtUTC);
    }

    [Fact]
    public async Task GetExhibitHistory_ReturnsEmptyList_WhenNoChanges()
    {
        var file = SeedFile();

        var history = await _service.GetExhibitHistoryAsync(file.Id);

        history.Should().BeEmpty();
    }

    [Fact]
    public async Task GetExhibitHistory_Throws_WhenFileNotFound()
    {
        var act = async () => await _service.GetExhibitHistoryAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Auto-accept (CES-39) ──────────────────────────────────────────────

    [Fact]
    public async Task MarkExhibit_AutoAcceptsFile_AndWritesMetadata()
    {
        var file = SeedFile();

        await _service.MarkExhibitAsync(file.Id, "A", "officer@test.ca");

        var dbFile = _db.StoredFiles.Find(file.Id)!;
        dbFile.IsAccepted.Should().BeTrue();
        dbFile.AcceptedAtUTC.Should().NotBeNull();
        dbFile.CanonicalPath.Should().NotBeNullOrEmpty();
        dbFile.Sha256.Should().Be("DEADBEEF");

        _fileStorageMock.Verify(s => s.PromoteToAcceptedAsync(It.IsAny<Submission>(), It.Is<StoredFiles>(f => f.Id == file.Id)), Times.Once);
        _fileStorageMock.Verify(s => s.WriteMetadataAsync(It.IsAny<Submission>(), It.IsAny<IReadOnlyList<SubmissionAuditLog>>()), Times.Once);
    }

    [Fact]
    public async Task EnterExhibit_AutoAcceptsFile_OnFirstEntered()
    {
        var file = SeedFile();

        await _service.EnterExhibitAsync(file.Id, "5", "officer@test.ca");

        _db.StoredFiles.Find(file.Id)!.IsAccepted.Should().BeTrue();
        _fileStorageMock.Verify(s => s.PromoteToAcceptedAsync(It.IsAny<Submission>(), It.IsAny<StoredFiles>()), Times.Once);
    }

    [Fact]
    public async Task MarkedThenEntered_PromotesOnlyOnce_KeepingSameSha()
    {
        var file = SeedFile();

        await _service.MarkExhibitAsync(file.Id, "A", "officer@test.ca");
        var shaAfterMark = _db.StoredFiles.Find(file.Id)!.Sha256;

        await _service.EnterExhibitAsync(file.Id, "3", "officer@test.ca");
        var shaAfterEnter = _db.StoredFiles.Find(file.Id)!.Sha256;

        // Bytes are immutable at accept: the second classification does not re-promote.
        _fileStorageMock.Verify(s => s.PromoteToAcceptedAsync(It.IsAny<Submission>(), It.IsAny<StoredFiles>()), Times.Once);
        shaAfterEnter.Should().Be(shaAfterMark);
        // ...but metadata is refreshed on both edits.
        _fileStorageMock.Verify(s => s.WriteMetadataAsync(It.IsAny<Submission>(), It.IsAny<IReadOnlyList<SubmissionAuditLog>>()), Times.Exactly(2));
    }

    [Fact]
    public async Task AddDescription_OnAcceptedNotEnteredFile_RewritesMetadata_WithoutPromoting()
    {
        var file = SeedFile();
        await _service.MarkExhibitAsync(file.Id, "A", "officer@test.ca"); // accept it
        _fileStorageMock.Invocations.Clear();

        await _service.AddExhibitDescriptionAsync(file.Id, "a note", "officer@test.ca");

        _fileStorageMock.Verify(s => s.PromoteToAcceptedAsync(It.IsAny<Submission>(), It.IsAny<StoredFiles>()), Times.Never);
        _fileStorageMock.Verify(s => s.WriteMetadataAsync(It.IsAny<Submission>(), It.IsAny<IReadOnlyList<SubmissionAuditLog>>()), Times.Once);
    }

    [Fact]
    public async Task AddDescription_OnUnacceptedFile_DoesNotPromoteOrWriteMetadata()
    {
        var file = SeedFile();

        await _service.AddExhibitDescriptionAsync(file.Id, "a note", "officer@test.ca");

        _db.StoredFiles.Find(file.Id)!.IsAccepted.Should().BeFalse();
        _fileStorageMock.Verify(s => s.PromoteToAcceptedAsync(It.IsAny<Submission>(), It.IsAny<StoredFiles>()), Times.Never);
        _fileStorageMock.Verify(s => s.WriteMetadataAsync(It.IsAny<Submission>(), It.IsAny<IReadOnlyList<SubmissionAuditLog>>()), Times.Never);
    }

    // ── GetExhibitContentAsync (download branch) ──────────────────────────

    [Fact]
    public async Task GetExhibitContent_AcceptedFile_ReadsFromCanonicalStore()
    {
        var file = SeedFile(isAccepted: true);
        using var accepted = new MemoryStream(new byte[] { 1, 2, 3 });
        _fileStorageMock.Setup(s => s.GetAcceptedExhibitAsync(It.Is<StoredFiles>(f => f.Id == file.Id))).ReturnsAsync(accepted);

        var (stream, fileName, contentType, error) = await _service.GetExhibitContentAsync(file.Id);

        stream.Should().NotBeNull();
        fileName.Should().Be("exhibit.mp4");
        contentType.Should().Be("video/mp4");
        error.Should().BeNull();
        _fileStorageMock.Verify(s => s.GetAcceptedExhibitAsync(It.IsAny<StoredFiles>()), Times.Once);
        _fileStorageMock.Verify(s => s.GetAsync(It.IsAny<StoredFiles>()), Times.Never);
    }

    [Fact]
    public async Task GetExhibitContent_PendingFile_ReadsFromTemporaryStore()
    {
        var file = SeedFile(); // not accepted
        using var pending = new MemoryStream(new byte[] { 9 });
        _fileStorageMock.Setup(s => s.GetAsync(It.Is<StoredFiles>(f => f.Id == file.Id))).ReturnsAsync(pending);

        var (stream, _, _, error) = await _service.GetExhibitContentAsync(file.Id);

        stream.Should().NotBeNull();
        error.Should().BeNull();
        _fileStorageMock.Verify(s => s.GetAsync(It.IsAny<StoredFiles>()), Times.Once);
        _fileStorageMock.Verify(s => s.GetAcceptedExhibitAsync(It.IsAny<StoredFiles>()), Times.Never);
    }

    [Fact]
    public async Task GetExhibitContent_ReturnsError_WhenFileMissingOrDeleted()
    {
        var missing = await _service.GetExhibitContentAsync(Guid.NewGuid());
        missing.stream.Should().BeNull();
        missing.error.Should().NotBeNullOrEmpty();

        var file = SeedFile();
        file.IsDeleted = true;
        await _db.SaveChangesAsync();

        var deleted = await _service.GetExhibitContentAsync(file.Id);
        deleted.stream.Should().BeNull();
        deleted.error.Should().NotBeNullOrEmpty();
    }

    // ── Admin override ────────────────────────────────────────────────────

    [Fact]
    public async Task MarkExhibit_AdminOverride_SucceedsOnEnteredFile()
    {
        var file = SeedFile(enteredValue: "5", enteredAt: DateTime.UtcNow);

        var result = await _service.MarkExhibitAsync(file.Id, "A", "admin@test.ca", isAdminOverride: true);

        result.MarkedValue.Should().Be("A");
        _db.SubmissionAuditLogs.Should().Contain(l => l.FieldName == "MarkedValue" && l.ChangedBy == "admin@test.ca");
    }

    [Fact]
    public async Task EnterExhibit_AdminOverride_SucceedsOutsideEditWindow()
    {
        var file = SeedFile(
            enteredValue: "3",
            enteredAt: DateTime.UtcNow - TimeSpan.FromSeconds(30));

        var result = await _service.EnterExhibitAsync(file.Id, "7", "admin@test.ca", isAdminOverride: true);

        result.EnteredValue.Should().Be("7");
        _db.SubmissionAuditLogs.Should().Contain(l => l.FieldName == "EnteredValue" && l.ChangedBy == "admin@test.ca");
    }

    [Fact]
    public async Task AddDescription_AdminOverride_SucceedsOnEnteredFile()
    {
        var file = SeedFile(enteredValue: "5", enteredAt: DateTime.UtcNow);

        var result = await _service.AddExhibitDescriptionAsync(file.Id, "admin note", "admin@test.ca", isAdminOverride: true);

        result.Descriptions.Should().ContainSingle()
            .Which.DescriptionText.Should().Be("admin note");
        _db.ExhibitDescriptions.Single(d => d.FileId == file.Id).CreatedBy.Should().Be("admin@test.ca");
    }

    [Fact]
    public async Task MarkExhibit_AdminOverride_StillEnforcesValueRange()
    {
        var file = SeedFile(enteredValue: "5", enteredAt: DateTime.UtcNow);

        var act = async () => await _service.MarkExhibitAsync(file.Id, "AA", "admin@test.ca", isAdminOverride: true);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task EnterExhibit_AdminOverride_StillEnforcesValueRange()
    {
        var file = SeedFile();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.EnterExhibitAsync(file.Id, "999", "admin@test.ca", isAdminOverride: true));
    }

    // ── Registry notes (CES-38 extension) ─────────────────────────────────

    [Fact]
    public async Task AddExhibitNote_PersistsImmutableNote()
    {
        var file = SeedFile();
        var before = DateTime.UtcNow;

        var result = await _service.AddExhibitNoteAsync(file.Id, "Registry note", "admin@test.ca");

        result.Id.Should().BeGreaterThan(0);
        result.NoteText.Should().Be("Registry note");
        result.CreatedBy.Should().Be("admin@test.ca");
        result.CreatedAtUTC.Should().BeOnOrAfter(before);
        _db.ExhibitNotes.Count(n => n.FileId == file.Id).Should().Be(1);
    }

    [Fact]
    public async Task AddExhibitNote_TrimsWhitespace()
    {
        var file = SeedFile();

        var result = await _service.AddExhibitNoteAsync(file.Id, "   spaced   ", "admin@test.ca");

        result.NoteText.Should().Be("spaced");
    }

    [Fact]
    public async Task AddExhibitNote_Rejects_WhenEmptyOrWhitespace()
    {
        var file = SeedFile();

        await Assert.ThrowsAsync<ArgumentException>(() => _service.AddExhibitNoteAsync(file.Id, "", "admin@test.ca"));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.AddExhibitNoteAsync(file.Id, "   ", "admin@test.ca"));
    }

    [Fact]
    public async Task AddExhibitNote_Rejects_WhenOverMaxLength()
    {
        var file = SeedFile();
        var tooLong = new string('x', 2001);

        var act = async () => await _service.AddExhibitNoteAsync(file.Id, tooLong, "admin@test.ca");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*2000*");
    }

    [Fact]
    public async Task AddExhibitNote_Throws_WhenFileNotFound()
    {
        var act = async () => await _service.AddExhibitNoteAsync(Guid.NewGuid(), "note", "admin@test.ca");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task AddExhibitNote_DoesNotWriteToChangeHistory()
    {
        // Registry notes are protected and must never surface in the exhibit's
        // field-change history (SubmissionAuditLog).
        var file = SeedFile();

        await _service.AddExhibitNoteAsync(file.Id, "protected note", "admin@test.ca");

        _db.SubmissionAuditLogs.Any(l => l.FileId == file.Id).Should().BeFalse();
        (await _service.GetExhibitHistoryAsync(file.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetExhibitNotes_ReturnsNotesOldestFirst()
    {
        var file = SeedFile();
        _db.ExhibitNotes.Add(new ExhibitNote
        {
            FileId = file.Id,
            NoteText = "first",
            CreatedBy = "admin@test.ca",
            CreatedAtUTC = new DateTime(2026, 7, 7, 9, 0, 0, DateTimeKind.Utc),
        });
        _db.ExhibitNotes.Add(new ExhibitNote
        {
            FileId = file.Id,
            NoteText = "second",
            CreatedBy = "admin@test.ca",
            CreatedAtUTC = new DateTime(2026, 7, 7, 10, 0, 0, DateTimeKind.Utc),
        });
        await _db.SaveChangesAsync();

        var notes = await _service.GetExhibitNotesAsync(file.Id);

        notes.Select(n => n.NoteText).Should().Equal("first", "second");
    }

    [Fact]
    public async Task GetExhibitNotes_ReturnsEmpty_WhenNone()
    {
        var file = SeedFile();

        var notes = await _service.GetExhibitNotesAsync(file.Id);

        notes.Should().BeEmpty();
    }

    [Fact]
    public async Task GetExhibitNotes_Throws_WhenFileNotFound()
    {
        var act = async () => await _service.GetExhibitNotesAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
