using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Business.Services;
using CES.EF;
using CES.Entities;
using CES.Entities.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CES.Business.Tests.Services;

public class SubmissionServiceTests : IDisposable
{
    private readonly CESDataStore _db;
    private readonly Mock<IFileStorage> _fileStorageMock;
    private readonly SubmissionService _service;

    public SubmissionServiceTests()
    {
        var options = new DbContextOptionsBuilder<CESDataStore>()
            .UseInMemoryDatabase($"SubmissionServiceTests_{Guid.NewGuid()}")
            .Options;
        _db = new CESDataStore(options);
        _fileStorageMock = new Mock<IFileStorage>();
        _service = new SubmissionService(_db, _fileStorageMock.Object);
    }

    public void Dispose() => _db.Dispose();

    private static EvidenceSubmissionModel BuildModel(int fileCount = 2, int ticketCount = 1) => new()
    {
        ShortDate = "2026-01-01",
        LocationId = "LOC001",
        LocationNameText = "Test Court",
        RoomCode = "ROOM1",
        RoomText = "Courtroom 1",
        OfficerNumber = "OFF001",
        Tickets = Enumerable.Range(0, ticketCount).Select(i => new SubmissionTicketModel
        {
            AppearanceId = $"APP{i:D3}",
            FileNumberText = $"FILE{i:D3}",
            AccusedName = "Smith, John",
            AppearanceDateTime = "2026-01-01T09:00:00",
        }).ToList(),
        fileUploads = Enumerable.Range(0, fileCount).Select(i => new FileUpload
        {
            FileName = $"file{i}.mp4",
            ContentType = "video/mp4",
            Length = 1024,
            Location = "LOC001",
            Date = "2026-01-01",
            Room = "ROOM1",
            Content = new MemoryStream(new byte[] { 0x01, 0x02 })
        }).ToList()
    };

    private static StoredFiles BuildStoredFile(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        OriginalFileName = "file.mp4",
        StoredFileName = "stored.mp4",
        StoredPath = "LOC001/2026-01-01/ROOM1/1",
        ContentType = "video/mp4",
        FileSize = 1024,
        StorageProvider = "Local"
    };

    private static Submission BuildSubmission(string fileNumber = "FILE001", string appearanceId = "APP001") => new()
    {
        ShortDate = "2026-01-01",
        LocationId = "LOC001",
        LocationNameText = "Test Court",
        RoomCode = "ROOM1",
        Tickets =
        [
            new SubmissionTicket
            {
                AppearanceId = appearanceId,
                FileNumberText = fileNumber,
                AccusedName = "Smith, John",
            }
        ]
    };

    // ── SubmitEvidence ────────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitEvidence_PersistsSubmissionAndFiles()
    {
        var model = BuildModel(fileCount: 2);
        _fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<FileUpload>(), It.IsAny<string>()))
            .ReturnsAsync((FileUpload _, string _) => BuildStoredFile());

        await _service.SubmitEvidence(model);

        _db.Submissions.Count().Should().Be(1);
        _db.StoredFiles.Count().Should().Be(2);
    }

    [Fact]
    public async Task SubmitEvidence_PersistsSubmissionTickets()
    {
        var model = BuildModel(ticketCount: 2);
        _fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<FileUpload>(), It.IsAny<string>()))
            .ReturnsAsync((FileUpload _, string _) => BuildStoredFile());

        await _service.SubmitEvidence(model);

        _db.SubmissionTickets.Count().Should().Be(2);
        _db.SubmissionTickets.Select(t => t.FileNumberText).Should().BeEquivalentTo(["FILE000", "FILE001"]);
    }

    [Fact]
    public async Task SubmitEvidence_UsesSubmissionIdInStoragePath()
    {
        var model = BuildModel(fileCount: 1);
        string? capturedPath = null;
        _fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<FileUpload>(), It.IsAny<string>()))
            .Callback<FileUpload, string>((_, path) => capturedPath = path)
            .ReturnsAsync((FileUpload _, string _) => BuildStoredFile());

        await _service.SubmitEvidence(model);

        var submission = _db.Submissions.First();
        var expectedPath = Path.Combine("LOC001", "2026-01-01", "ROOM1", submission.Id.ToString());
        capturedPath.Should().Be(expectedPath);
    }

    [Fact]
    public async Task SubmitEvidence_RejectsMissingTickets()
    {
        var model = BuildModel();
        model.Tickets = [];
        _fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<FileUpload>(), It.IsAny<string>()))
            .ReturnsAsync((FileUpload _, string _) => BuildStoredFile());

        var result = await _service.SubmitEvidence(model);

        result.Should().BeNull();
        _db.Submissions.Count().Should().Be(0);
    }

    [Fact]
    public async Task SubmitEvidence_ReturnsSubmissionId()
    {
        var model = BuildModel(fileCount: 1);
        _fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<FileUpload>(), It.IsAny<string>()))
            .ReturnsAsync((FileUpload _, string _) => BuildStoredFile());

        var result = await _service.SubmitEvidence(model);

        result.Should().NotBeNull();
        result.Should().Be(_db.Submissions.First().Id);
    }

    [Fact]
    public async Task SubmitEvidence_PersistsShortDateAndAppearanceDateTime()
    {
        var model = BuildModel(fileCount: 1);
        model.AppearanceDateTime = "2026-01-01T09:00:00";
        _fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<FileUpload>(), It.IsAny<string>()))
            .ReturnsAsync((FileUpload _, string _) => BuildStoredFile());

        await _service.SubmitEvidence(model);

        var submission = _db.Submissions.First();
        submission.ShortDate.Should().Be("2026-01-01");
        submission.AppearanceDateTime.Should().Be("2026-01-01T09:00:00");
    }

    [Fact]
    public async Task SubmitEvidence_AppendsToExistingSubmission_WhenValidSubmissionIdProvided()
    {
        // Seed an existing Pending submission with one file.
        var existing = BuildSubmission("FILE000", "APP000");
        existing.Files.Add(BuildStoredFile());
        _db.Submissions.Add(existing);
        await _db.SaveChangesAsync();

        var model = BuildModel(fileCount: 2, ticketCount: 1);
        model.SubmissionId = existing.Id;
        _fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<FileUpload>(), It.IsAny<string>()))
            .ReturnsAsync((FileUpload _, string _) => BuildStoredFile());

        var result = await _service.SubmitEvidence(model);

        result.Should().Be(existing.Id);
        // No new submission and no duplicate tickets — only files were appended.
        _db.Submissions.Count().Should().Be(1);
        _db.SubmissionTickets.Count().Should().Be(1);
        _db.StoredFiles.Count(f => f.SubmissionId == existing.Id).Should().Be(3);
    }

    [Fact]
    public async Task SubmitEvidence_AppendUsesExistingSubmissionStoragePath()
    {
        var existing = BuildSubmission();
        _db.Submissions.Add(existing);
        await _db.SaveChangesAsync();

        var model = BuildModel(fileCount: 1);
        model.SubmissionId = existing.Id;
        string? capturedPath = null;
        _fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<FileUpload>(), It.IsAny<string>()))
            .Callback<FileUpload, string>((_, path) => capturedPath = path)
            .ReturnsAsync((FileUpload _, string _) => BuildStoredFile());

        await _service.SubmitEvidence(model);

        capturedPath.Should().Be(Path.Combine("LOC001", "2026-01-01", "ROOM1", existing.Id.ToString()));
    }

    [Fact]
    public async Task SubmitEvidence_FallsBackToNew_WhenSubmissionRejected()
    {
        // Rejected is terminal — a new upload with that id starts a fresh submission
        // rather than re-opening the rejected one (CES-39). Accepted submissions, by
        // contrast, are appendable and reopen to Pending (covered separately).
        var existing = BuildSubmission();
        existing.Status = SubmissionStatus.Rejected;
        _db.Submissions.Add(existing);
        await _db.SaveChangesAsync();

        var model = BuildModel(fileCount: 1);
        model.SubmissionId = existing.Id;
        _fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<FileUpload>(), It.IsAny<string>()))
            .ReturnsAsync((FileUpload _, string _) => BuildStoredFile());

        var result = await _service.SubmitEvidence(model);

        result.Should().NotBe(existing.Id);
        _db.Submissions.Count().Should().Be(2);
    }

    [Fact]
    public async Task SubmitEvidence_FallsBackToNew_WhenLocationMismatch()
    {
        var existing = BuildSubmission();
        existing.LocationId = "OTHER";
        _db.Submissions.Add(existing);
        await _db.SaveChangesAsync();

        var model = BuildModel(fileCount: 1); // LocationId = LOC001
        model.SubmissionId = existing.Id;
        _fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<FileUpload>(), It.IsAny<string>()))
            .ReturnsAsync((FileUpload _, string _) => BuildStoredFile());

        var result = await _service.SubmitEvidence(model);

        result.Should().NotBe(existing.Id);
        _db.Submissions.Count().Should().Be(2);
    }

    [Fact]
    public async Task SubmitEvidence_FallsBackToNew_WhenSubmissionIdNotFound()
    {
        var model = BuildModel(fileCount: 1);
        model.SubmissionId = 99999;
        _fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<FileUpload>(), It.IsAny<string>()))
            .ReturnsAsync((FileUpload _, string _) => BuildStoredFile());

        var result = await _service.SubmitEvidence(model);

        result.Should().NotBeNull();
        _db.Submissions.Count().Should().Be(1);
    }

    [Fact]
    public async Task SubmitEvidence_CallsFileStorageSaveAsync()
    {
        var model = BuildModel(fileCount: 2);
        _fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<FileUpload>(), It.IsAny<string>()))
            .ReturnsAsync((FileUpload _, string _) => BuildStoredFile());

        await _service.SubmitEvidence(model);

        _fileStorageMock.Verify(
            s => s.SaveAsync(It.IsAny<FileUpload>(), It.IsAny<string>()),
            Times.Exactly(2));
    }

    // ── RetrieveSubmission ────────────────────────────────────────────────────

    [Fact]
    public async Task RetrieveSubmission_ReturnsModel_WhenExists()
    {
        var submission = BuildSubmission("FILE001");
        submission.Files.Add(BuildStoredFile());
        submission.Files.Add(BuildStoredFile());
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        var result = await _service.RetrieveSubmission(submission.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(submission.Id);
        result.Tickets.Should().HaveCount(1);
        result.Tickets[0].FileNumberText.Should().Be("FILE001");
        result.Files.Should().HaveCount(2);
    }

    [Fact]
    public async Task RetrieveSubmission_ReturnsNull_WhenNotFound()
    {
        var result = await _service.RetrieveSubmission(99999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RetrieveSubmission_IncludesRemovedFiles()
    {
        var submission = BuildSubmission();
        var active = BuildStoredFile();
        var removed = BuildStoredFile();
        removed.IsDeleted = true;
        removed.DeletedAtUTC = DateTime.UtcNow;
        submission.Files.Add(active);
        submission.Files.Add(removed);
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        var result = await _service.RetrieveSubmission(submission.Id);

        result!.Files.Should().HaveCount(2);
        result.Files.Should().Contain(f => f.Status == "Removed");
    }

    // ── RetrieveSubmissionListing ─────────────────────────────────────────────

    [Fact]
    public async Task RetrieveSubmissionListing_ReturnsAllStatuses()
    {
        var pending = BuildSubmission("F1");
        var accepted = BuildSubmission("F2");
        accepted.Status = SubmissionStatus.Accepted;
        var rejected = BuildSubmission("F3");
        rejected.Status = SubmissionStatus.Rejected;
        _db.Submissions.AddRange(pending, accepted, rejected);
        await _db.SaveChangesAsync();

        var result = await _service.RetrieveSubmissionListing(new SubmissionListFilter());

        result.TotalCount.Should().Be(3);
        result.Items.Select(i => i.Status).Should().BeEquivalentTo(["Pending", "Accepted", "Rejected"], o => o.WithoutStrictOrdering());
    }

    [Fact]
    public async Task RetrieveSubmissionListing_DoesNotHideAcceptedSubmissions()
    {
        var pending = BuildSubmission("F1");
        var accepted = BuildSubmission("F2");
        accepted.Status = SubmissionStatus.Accepted;
        _db.Submissions.AddRange(pending, accepted);
        await _db.SaveChangesAsync();

        var result = await _service.RetrieveSubmissionListing(new SubmissionListFilter());

        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task RetrieveSubmissionListing_IncludesTicketsInResponse()
    {
        var sub = BuildSubmission("FILE001");
        _db.Submissions.Add(sub);
        await _db.SaveChangesAsync();

        var result = await _service.RetrieveSubmissionListing(new SubmissionListFilter());

        result.Items.Should().HaveCount(1);
        result.Items[0].Tickets.Should().HaveCount(1);
        result.Items[0].Tickets[0].FileNumberText.Should().Be("FILE001");
    }

    [Fact]
    public async Task RetrieveSubmissionListing_FiltersByStatus()
    {
        var pending = BuildSubmission("F1");
        var accepted = BuildSubmission("F2");
        accepted.Status = SubmissionStatus.Accepted;
        _db.Submissions.AddRange(pending, accepted);
        await _db.SaveChangesAsync();

        var result = await _service.RetrieveSubmissionListing(new SubmissionListFilter { Status = SubmissionStatus.Pending });

        result.TotalCount.Should().Be(1);
        result.Items[0].Status.Should().Be("Pending");
    }

    [Fact]
    public async Task RetrieveSubmissionListing_FiltersByFileNumber()
    {
        var match = BuildSubmission("FILE001");
        var other = BuildSubmission("FILE999");
        _db.Submissions.AddRange(match, other);
        await _db.SaveChangesAsync();

        var result = await _service.RetrieveSubmissionListing(new SubmissionListFilter { FileNumberText = "FILE001" });

        result.TotalCount.Should().Be(1);
        result.Items[0].Tickets[0].FileNumberText.Should().Be("FILE001");
    }

    [Fact]
    public async Task RetrieveSubmissionListing_FiltersByAccusedName_CaseInsensitive()
    {
        var match = BuildSubmission("F1");
        var other = BuildSubmission("F2");
        other.Tickets.First().AccusedName = "Jones, Mary";
        _db.Submissions.AddRange(match, other);
        await _db.SaveChangesAsync();

        var result = await _service.RetrieveSubmissionListing(new SubmissionListFilter { AccusedName = "smith" });

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task RetrieveSubmissionListing_Paging_ReturnsCorrectPage()
    {
        for (int i = 0; i < 5; i++)
            _db.Submissions.Add(BuildSubmission($"F{i}"));
        await _db.SaveChangesAsync();

        var result = await _service.RetrieveSubmissionListing(new SubmissionListFilter { Page = 2, PageSize = 2 });

        result.TotalCount.Should().Be(5);
        result.Items.Should().HaveCount(2);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task RetrieveSubmissionListing_Paging_OutOfRangePageReturnsEmpty()
    {
        _db.Submissions.Add(BuildSubmission("F1"));
        await _db.SaveChangesAsync();

        var result = await _service.RetrieveSubmissionListing(new SubmissionListFilter { Page = 99, PageSize = 10 });

        result.TotalCount.Should().Be(1);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task RetrieveSubmissionListing_Paging_ClampsPageSizeToMax()
    {
        _db.Submissions.Add(BuildSubmission("F1"));
        await _db.SaveChangesAsync();

        var result = await _service.RetrieveSubmissionListing(new SubmissionListFilter { PageSize = 9999 });

        result.PageSize.Should().Be(CES.Business.Constants.PagingConstants.MaxPageSize);
    }

    [Fact]
    public async Task RetrieveSubmissionListing_ExhibitCount_CountsActiveFilesOnly()
    {
        var sub = BuildSubmission("F1");
        var active = BuildStoredFile();
        var removed = BuildStoredFile();
        removed.IsDeleted = true;
        sub.Files.Add(active);
        sub.Files.Add(removed);
        _db.Submissions.Add(sub);
        await _db.SaveChangesAsync();

        var result = await _service.RetrieveSubmissionListing(new SubmissionListFilter());

        result.Items[0].ExhibitCount.Should().Be(1);
    }

    // ── Derived submission status (CES-39) ─────────────────────────────────────

    [Fact]
    public async Task SubmitEvidence_NewSubmissionWithUnacceptedFiles_StaysPending()
    {
        _fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<FileUpload>(), It.IsAny<string>()))
            .ReturnsAsync((FileUpload f, string _) => new StoredFiles
            {
                Id = Guid.NewGuid(),
                OriginalFileName = f.FileName,
                StoredFileName = $"{Guid.NewGuid()}.mp4",
                StoredPath = "p",
                ContentType = f.ContentType,
                FileSize = f.Length,
                StorageProvider = "Mock",
            });

        var id = await _service.SubmitEvidence(BuildModel());

        id.Should().NotBeNull();
        _db.Submissions.Find(id!.Value)!.Status.Should().Be(SubmissionStatus.Pending);
    }

    [Fact]
    public async Task SubmitEvidence_AddingFileToAcceptedSubmission_FlipsBackToPending()
    {
        // An already-Accepted submission (all existing files accepted) gains a new
        // same-session upload — it should reopen to Pending until that file is accepted.
        var submission = BuildSubmission();
        submission.Status = SubmissionStatus.Accepted;
        var accepted = BuildStoredFile();
        accepted.IsAccepted = true;
        submission.Files.Add(accepted);
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        _fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<FileUpload>(), It.IsAny<string>()))
            .ReturnsAsync((FileUpload f, string _) => new StoredFiles
            {
                Id = Guid.NewGuid(),
                OriginalFileName = f.FileName,
                StoredFileName = $"{Guid.NewGuid()}.mp4",
                StoredPath = "p",
                ContentType = f.ContentType,
                FileSize = f.Length,
                StorageProvider = "Mock",
            });

        var model = BuildModel(fileCount: 1);
        model.SubmissionId = submission.Id;
        model.ShortDate = submission.ShortDate;

        await _service.SubmitEvidence(model);

        _db.Submissions.Find(submission.Id)!.Status.Should().Be(SubmissionStatus.Pending);
    }

    // ── RejectSubmissions ─────────────────────────────────────────────────────

    [Fact]
    public async Task RejectSubmissions_SetsStatusRejected_AndDeletesUnacceptedFiles()
    {
        var submission = BuildSubmission();
        submission.Files.Add(BuildStoredFile());
        submission.Files.Add(BuildStoredFile());
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        _fileStorageMock.Setup(s => s.DeleteAsync(It.IsAny<StoredFiles>())).Returns(Task.CompletedTask);

        var (success, error) = await _service.RejectSubmissions(new SubmissionActionModel { SubmissionId = submission.Id });

        success.Should().BeTrue();
        error.Should().BeNull();
        _db.Submissions.Find(submission.Id)!.Status.Should().Be(SubmissionStatus.Rejected);
        _db.Submissions.Find(submission.Id)!.IsDeleted.Should().BeFalse();
        _db.StoredFiles.Where(f => f.SubmissionId == submission.Id).All(f => f.IsDeleted).Should().BeTrue();
        _db.StoredFiles.Where(f => f.SubmissionId == submission.Id).All(f => f.DeletedAtUTC != null).Should().BeTrue();
        _fileStorageMock.Verify(s => s.DeleteAsync(It.IsAny<StoredFiles>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RejectSubmissions_LeavesAcceptedFiles_ButDeletesUnaccepted()
    {
        var submission = BuildSubmission();
        var accepted = BuildStoredFile();
        accepted.IsAccepted = true;
        var pending = BuildStoredFile();
        submission.Files.Add(accepted);
        submission.Files.Add(pending);
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        _fileStorageMock.Setup(s => s.DeleteAsync(It.IsAny<StoredFiles>())).Returns(Task.CompletedTask);

        var (success, _) = await _service.RejectSubmissions(new SubmissionActionModel { SubmissionId = submission.Id });

        success.Should().BeTrue();
        _db.StoredFiles.Find(accepted.Id)!.IsDeleted.Should().BeFalse();
        _db.StoredFiles.Find(pending.Id)!.IsDeleted.Should().BeTrue();
        // Only the non-accepted file's bytes are deleted from storage.
        _fileStorageMock.Verify(s => s.DeleteAsync(It.Is<StoredFiles>(f => f.Id == pending.Id)), Times.Once);
        _fileStorageMock.Verify(s => s.DeleteAsync(It.Is<StoredFiles>(f => f.Id == accepted.Id)), Times.Never);
    }

    [Fact]
    public async Task RejectSubmissions_RejectsAlreadyRejectedSubmission()
    {
        var submission = BuildSubmission();
        submission.Status = SubmissionStatus.Rejected;
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        var (success, error) = await _service.RejectSubmissions(new SubmissionActionModel { SubmissionId = submission.Id });

        success.Should().BeFalse();
        error.Should().Contain("already rejected");
    }

    // ── RemoveFile ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveFile_MarksFileDeleted_AndRecordsDeletionTimestamp()
    {
        var submission = BuildSubmission();
        var file = BuildStoredFile();
        submission.Files.Add(file);
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        _fileStorageMock.Setup(s => s.DeleteAsync(It.IsAny<StoredFiles>())).Returns(Task.CompletedTask);

        var result = await _service.RemoveFileAsync(file.Id);

        result.Should().BeTrue();
        var dbFile = _db.StoredFiles.Find(file.Id)!;
        dbFile.IsDeleted.Should().BeTrue();
        dbFile.DeletedAtUTC.Should().NotBeNull();
        _fileStorageMock.Verify(s => s.DeleteAsync(It.Is<StoredFiles>(f => f.Id == file.Id)), Times.Once);
    }

    [Fact]
    public async Task RemoveFile_SucceedsOnEnteredExhibit_WhenPending()
    {
        var submission = BuildSubmission();
        var file = BuildStoredFile();
        file.EnteredValue = "5";
        file.EnteredAt = DateTime.UtcNow;
        submission.Files.Add(file);
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        _fileStorageMock.Setup(s => s.DeleteAsync(It.IsAny<StoredFiles>())).Returns(Task.CompletedTask);

        var result = await _service.RemoveFileAsync(file.Id);

        result.Should().BeTrue();
        _db.StoredFiles.Find(file.Id)!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveFile_RejectsWhenFileIsAccepted()
    {
        // Per-file accept means a Pending submission can hold accepted files; an
        // accepted file can never be removed (CES-39, Q6).
        var submission = BuildSubmission();
        var file = BuildStoredFile();
        file.IsAccepted = true;
        submission.Files.Add(file);
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        _fileStorageMock.Setup(s => s.DeleteAsync(It.IsAny<StoredFiles>())).Returns(Task.CompletedTask);

        var act = async () => await _service.RemoveFileAsync(file.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Accepted*");
        _fileStorageMock.Verify(s => s.DeleteAsync(It.IsAny<StoredFiles>()), Times.Never);
    }

    [Fact]
    public async Task RemoveFile_RejectsWhenSubmissionIsRejected()
    {
        var submission = BuildSubmission();
        submission.Status = SubmissionStatus.Rejected;
        var file = BuildStoredFile();
        submission.Files.Add(file);
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        _fileStorageMock.Setup(s => s.DeleteAsync(It.IsAny<StoredFiles>())).Returns(Task.CompletedTask);

        var act = async () => await _service.RemoveFileAsync(file.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*rejected*");
        _fileStorageMock.Verify(s => s.DeleteAsync(It.IsAny<StoredFiles>()), Times.Never);
    }

    [Fact]
    public async Task RemoveFile_ReturnsFalse_WhenFileNotFound()
    {
        _fileStorageMock.Setup(s => s.DeleteAsync(It.IsAny<StoredFiles>())).Returns(Task.CompletedTask);

        var result = await _service.RemoveFileAsync(Guid.NewGuid());

        result.Should().BeFalse();
        _fileStorageMock.Verify(s => s.DeleteAsync(It.IsAny<StoredFiles>()), Times.Never);
    }

    [Fact]
    public async Task RemoveFile_ReturnsFalse_WhenFileAlreadyDeleted()
    {
        var fileId = Guid.NewGuid();
        var file = BuildStoredFile(fileId);
        file.IsDeleted = true;
        _db.StoredFiles.Add(file);
        await _db.SaveChangesAsync();

        _fileStorageMock.Setup(s => s.DeleteAsync(It.IsAny<StoredFiles>())).Returns(Task.CompletedTask);

        var result = await _service.RemoveFileAsync(fileId);

        result.Should().BeFalse();
        _fileStorageMock.Verify(s => s.DeleteAsync(It.IsAny<StoredFiles>()), Times.Never);
    }

    // ── GetSubmissionsByFileNumber ─────────────────────────────────────────────

    [Fact]
    public async Task GetSubmissionsByFileNumber_ReturnsMatchingSubmissions()
    {
        var sub1 = BuildSubmission("FILE001");
        var sub2 = BuildSubmission("FILE001");
        sub2.LocationId = "LOC002";
        var sub3 = BuildSubmission("FILE999");
        _db.Submissions.AddRange(sub1, sub2, sub3);
        await _db.SaveChangesAsync();

        var result = await _service.GetSubmissionsByFileNumberAsync("FILE001");

        result.Should().HaveCount(2);
        result.All(r => r.Files.Count == 0).Should().BeTrue();
    }

    [Fact]
    public async Task GetSubmissionsByFileNumber_ExcludesDeletedSubmissions()
    {
        var active = BuildSubmission("FILE001");
        var deleted = BuildSubmission("FILE001");
        deleted.IsDeleted = true;
        _db.Submissions.AddRange(active, deleted);
        await _db.SaveChangesAsync();

        var result = await _service.GetSubmissionsByFileNumberAsync("FILE001");

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetSubmissionsByFileNumber_ReturnsEmptyForUnknownFileNumber()
    {
        var result = await _service.GetSubmissionsByFileNumberAsync("UNKNOWN");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSubmissionsByFileNumber_IncludesClassificationFields()
    {
        var submission = BuildSubmission("FILE001");
        var file = BuildStoredFile();
        file.MarkedValue = "B";
        file.MarkedAt = DateTime.UtcNow;
        file.EnteredValue = "4";
        file.EnteredAt = DateTime.UtcNow;
        file.Descriptions.Add(new ExhibitDescription { DescriptionText = "test exhibit", CreatedBy = "officer@test.ca" });
        submission.Files.Add(file);
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        var result = await _service.GetSubmissionsByFileNumberAsync("FILE001");

        var resultFile = result.First().Files.First();
        resultFile.MarkedValue.Should().Be("B");
        resultFile.EnteredValue.Should().Be("4");
        resultFile.Descriptions.Should().ContainSingle()
            .Which.DescriptionText.Should().Be("test exhibit");
        resultFile.Status.Should().Be("Entered");
    }

    // Per-file download is covered in FileServiceTests (GetExhibitContentAsync) and
    // the controller integration tests. The whole-submission ZIP package is retired
    // (CES-39, Phase 6).

    // ── SearchExhibitsAsync (CES-38) ──────────────────────────────────────────

    private static StoredFiles ClassifiedFile(string? marked = null, string? entered = null, bool removed = false)
    {
        var f = BuildStoredFile();
        f.MarkedValue = marked;
        f.EnteredValue = entered;
        f.IsDeleted = removed;
        return f;
    }

    [Fact]
    public async Task SearchExhibits_FileNumberContainsMatch_ReturnsExhibitsAcrossSubmissions()
    {
        var sub1 = BuildSubmission("AH123456789-1", "APP001");
        sub1.Files.Add(BuildStoredFile());
        var sub2 = BuildSubmission("AH123456789-2", "APP002");
        sub2.Files.Add(BuildStoredFile());
        var other = BuildSubmission("ZZ999888", "APP003");
        other.Files.Add(BuildStoredFile());
        _db.Submissions.AddRange(sub1, sub2, other);
        await _db.SaveChangesAsync();

        var result = await _service.SearchExhibitsAsync(new ExhibitSearchFilter { FileNumberText = "AH1234" });

        result.Should().HaveCount(2);
        result.Select(r => r.SubmissionId).Should().BeEquivalentTo(new[] { sub1.Id, sub2.Id });
    }

    [Fact]
    public async Task SearchExhibits_LastNameContainsMatch_FiltersByAccusedName()
    {
        var smith = BuildSubmission("FILE001", "APP001");
        smith.Files.Add(BuildStoredFile());
        var jones = BuildSubmission("FILE002", "APP002");
        jones.Tickets.First().AccusedName = "Jones, Mary";
        jones.Files.Add(BuildStoredFile());
        _db.Submissions.AddRange(smith, jones);
        await _db.SaveChangesAsync();

        var result = await _service.SearchExhibitsAsync(new ExhibitSearchFilter { AccusedName = "smith" });

        result.Should().HaveCount(1);
        result[0].SubmissionId.Should().Be(smith.Id);
        result[0].AccusedName.Should().Be("Smith, John");
    }

    [Fact]
    public async Task SearchExhibits_DateRange_FiltersOnAppearanceDate()
    {
        var july = BuildSubmission("SHARED123", "APP001");
        july.Tickets.First().AppearanceDateTime = "2026-07-07T09:00:00";
        july.Files.Add(BuildStoredFile());
        var august = BuildSubmission("SHARED123", "APP002");
        august.Tickets.First().AppearanceDateTime = "2026-08-01T09:00:00";
        august.Files.Add(BuildStoredFile());
        _db.Submissions.AddRange(july, august);
        await _db.SaveChangesAsync();

        var result = await _service.SearchExhibitsAsync(new ExhibitSearchFilter
        {
            FileNumberText = "SHARED123",
            AppearanceDateFrom = new DateTime(2026, 7, 1),
            AppearanceDateTo = new DateTime(2026, 7, 31),
        });

        result.Should().HaveCount(1);
        result[0].SubmissionId.Should().Be(july.Id);
    }

    [Fact]
    public async Task SearchExhibits_SortOrder_MarkedThenEnteredThenUnclassified()
    {
        var sub = BuildSubmission("FILE001");
        sub.Files.Add(ClassifiedFile(marked: "B"));
        sub.Files.Add(ClassifiedFile(marked: "A"));
        sub.Files.Add(ClassifiedFile(entered: "10"));
        sub.Files.Add(ClassifiedFile(entered: "2"));
        // Marked + Entered → sorts in the Entered group by its number (terminal state).
        sub.Files.Add(ClassifiedFile(marked: "Z", entered: "5"));
        sub.Files.Add(ClassifiedFile());
        _db.Submissions.Add(sub);
        await _db.SaveChangesAsync();

        var result = await _service.SearchExhibitsAsync(new ExhibitSearchFilter { FileNumberText = "FILE001" });

        var order = result
            .Select(r => r.File.EnteredValue ?? r.File.MarkedValue ?? "-")
            .ToList();
        order.Should().Equal("A", "B", "2", "5", "10", "-");
    }

    [Fact]
    public async Task SearchExhibits_ExcludesRemovedFiles()
    {
        var sub = BuildSubmission("FILE001");
        sub.Files.Add(BuildStoredFile());
        sub.Files.Add(ClassifiedFile(removed: true));
        _db.Submissions.Add(sub);
        await _db.SaveChangesAsync();

        var result = await _service.SearchExhibitsAsync(new ExhibitSearchFilter { FileNumberText = "FILE001" });

        result.Should().HaveCount(1);
        result[0].File.Status.Should().NotBe("Removed");
    }

    [Fact]
    public async Task SearchExhibits_ExcludesDeletedSubmissions()
    {
        var active = BuildSubmission("FILE001");
        active.Files.Add(BuildStoredFile());
        var deleted = BuildSubmission("FILE001");
        deleted.IsDeleted = true;
        deleted.Files.Add(BuildStoredFile());
        _db.Submissions.AddRange(active, deleted);
        await _db.SaveChangesAsync();

        var result = await _service.SearchExhibitsAsync(new ExhibitSearchFilter { FileNumberText = "FILE001" });

        result.Should().HaveCount(1);
        result[0].SubmissionId.Should().Be(active.Id);
    }

    [Fact]
    public async Task SearchExhibits_PopulatesSubmissionContext()
    {
        var sub = BuildSubmission("FILE001", "APP001");
        sub.RoomText = "Courtroom 1";
        sub.Tickets.First().AppearanceDateTime = "2026-07-07T09:00:00";
        sub.Files.Add(BuildStoredFile());
        _db.Submissions.Add(sub);
        await _db.SaveChangesAsync();

        var result = await _service.SearchExhibitsAsync(new ExhibitSearchFilter { FileNumberText = "FILE001" });

        var row = result.Single();
        row.SubmissionId.Should().Be(sub.Id);
        row.Location.Should().Be("Test Court");
        row.Room.Should().Be("Courtroom 1");
        row.AppearanceDateTime.Should().Be("2026-07-07T09:00:00");
        row.FileNumbers.Should().Equal("FILE001");
        row.SubmissionDate.Should().NotBeNull();
    }
}
