using CES.Business.Extensions.Entities;
using CES.Business.Services;
using CES.EF;
using CES.Entities;
using CES.Entities.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CES.Business.Tests.Services;

public class FileServiceTests : IDisposable
{
    private readonly CESDataStore _db;
    private readonly FileService _service;

    public FileServiceTests()
    {
        var options = new DbContextOptionsBuilder<CESDataStore>()
            .UseInMemoryDatabase($"FileServiceTests_{Guid.NewGuid()}")
            .Options;
        _db = new CESDataStore(options);
        _service = new FileService(_db);
    }

    public void Dispose() => _db.Dispose();

    private StoredFiles SeedFile(
        Guid? id = null,
        string? markedValue = null,
        string? enteredValue = null,
        DateTime? enteredAt = null)
    {
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
        };
        _db.StoredFiles.Add(file);
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

    // ── UpdateExhibitDescriptionAsync ─────────────────────────────────────

    [Fact]
    public async Task UpdateDescription_PersistsAndAudits()
    {
        var file = SeedFile();

        var result = await _service.UpdateExhibitDescriptionAsync(file.Id, "key piece of evidence", "officer@test.ca");

        var dbFile = _db.StoredFiles.Find(file.Id)!;
        dbFile.Description.Should().Be("key piece of evidence");

        var log = _db.SubmissionAuditLogs.Single(l => l.FileId == file.Id && l.FieldName == "Description");
        log.NewValue.Should().Be("key piece of evidence");
        log.ChangedBy.Should().Be("officer@test.ca");

        result.Description.Should().Be("key piece of evidence");
    }

    [Fact]
    public async Task UpdateDescription_Rejects_WhenEntered()
    {
        var file = SeedFile(enteredValue: "5", enteredAt: DateTime.UtcNow);

        var act = async () => await _service.UpdateExhibitDescriptionAsync(file.Id, "notes", "officer@test.ca");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Entered*");
    }

    [Fact]
    public async Task UpdateDescription_Rejects_WhenOverMaxLength()
    {
        var file = SeedFile();
        var tooLong = new string('x', 251);

        var act = async () => await _service.UpdateExhibitDescriptionAsync(file.Id, tooLong, "officer@test.ca");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*250*");
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
    public async Task UpdateDescription_AdminOverride_SucceedsOnEnteredFile()
    {
        var file = SeedFile(enteredValue: "5", enteredAt: DateTime.UtcNow);

        var result = await _service.UpdateExhibitDescriptionAsync(file.Id, "admin note", "admin@test.ca", isAdminOverride: true);

        result.Description.Should().Be("admin note");
        _db.SubmissionAuditLogs.Should().Contain(l => l.FieldName == "Description" && l.ChangedBy == "admin@test.ca");
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
}
