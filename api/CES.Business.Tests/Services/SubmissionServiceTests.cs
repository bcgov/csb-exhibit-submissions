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

        result.Should().BeFalse();
        _db.Submissions.Count().Should().Be(0);
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

    // ── AcceptSubmissions ─────────────────────────────────────────────────────

    [Fact]
    public async Task AcceptSubmissions_SetsStatusAccepted_WhenAllExhibitsReady()
    {
        var submission = BuildSubmission();
        var file = BuildStoredFile();
        file.EnteredValue = "3";
        submission.Files.Add(file);
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        _fileStorageMock.Setup(s => s.AcceptSubmissionAsync(It.IsAny<Submission>())).Returns(Task.CompletedTask);

        var (success, error) = await _service.AcceptSubmissions(new SubmissionActionModel { SubmissionId = submission.Id });

        success.Should().BeTrue();
        error.Should().BeNull();
        _db.Submissions.Find(submission.Id)!.Status.Should().Be(SubmissionStatus.Accepted);
        _db.Submissions.Find(submission.Id)!.StatusChangedDateUTC.Should().NotBeNull();
    }

    [Fact]
    public async Task AcceptSubmissions_DoesNotSetIsDeleted_OnSubmissionOrFiles()
    {
        var submission = BuildSubmission();
        var file = BuildStoredFile();
        file.EnteredValue = "3";
        submission.Files.Add(file);
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        _fileStorageMock.Setup(s => s.AcceptSubmissionAsync(It.IsAny<Submission>())).Returns(Task.CompletedTask);

        await _service.AcceptSubmissions(new SubmissionActionModel { SubmissionId = submission.Id });

        _db.Submissions.Find(submission.Id)!.IsDeleted.Should().BeFalse();
        _db.StoredFiles.Where(f => f.SubmissionId == submission.Id).All(f => !f.IsDeleted).Should().BeTrue();
    }

    [Fact]
    public async Task AcceptSubmissions_AcceptsRemovedExhibits_AsReady()
    {
        var submission = BuildSubmission();
        var entered = BuildStoredFile();
        entered.EnteredValue = "1";
        var removed = BuildStoredFile();
        removed.IsDeleted = true;
        submission.Files.Add(entered);
        submission.Files.Add(removed);
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        _fileStorageMock.Setup(s => s.AcceptSubmissionAsync(It.IsAny<Submission>())).Returns(Task.CompletedTask);

        var (success, _) = await _service.AcceptSubmissions(new SubmissionActionModel { SubmissionId = submission.Id });

        success.Should().BeTrue();
    }

    [Fact]
    public async Task AcceptSubmissions_RejectsWhenExhibitIsUnclassified()
    {
        var submission = BuildSubmission();
        submission.Files.Add(BuildStoredFile()); // unclassified
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        var (success, error) = await _service.AcceptSubmissions(new SubmissionActionModel { SubmissionId = submission.Id });

        success.Should().BeFalse();
        error.Should().Contain("Unready");
    }

    [Fact]
    public async Task AcceptSubmissions_RejectsWhenExhibitIsMarkedOnly()
    {
        var submission = BuildSubmission();
        var file = BuildStoredFile();
        file.MarkedValue = "A";
        submission.Files.Add(file);
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        var (success, error) = await _service.AcceptSubmissions(new SubmissionActionModel { SubmissionId = submission.Id });

        success.Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AcceptSubmissions_RejectsTerminalSubmission()
    {
        var submission = BuildSubmission();
        submission.Status = SubmissionStatus.Accepted;
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        var (success, error) = await _service.AcceptSubmissions(new SubmissionActionModel { SubmissionId = submission.Id });

        success.Should().BeFalse();
        error.Should().Contain("Pending");
    }

    [Fact]
    public async Task AcceptSubmissions_ReturnsFailure_WhenNotFound()
    {
        var (success, error) = await _service.AcceptSubmissions(new SubmissionActionModel { SubmissionId = 99999 });

        success.Should().BeFalse();
        error.Should().Contain("not found");
    }

    // ── RejectSubmissions ─────────────────────────────────────────────────────

    [Fact]
    public async Task RejectSubmissions_SetsStatusRejected_AndDeletesFiles()
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
    public async Task RejectSubmissions_RejectsTerminalSubmission()
    {
        var submission = BuildSubmission();
        submission.Status = SubmissionStatus.Rejected;
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        var (success, error) = await _service.RejectSubmissions(new SubmissionActionModel { SubmissionId = submission.Id });

        success.Should().BeFalse();
        error.Should().Contain("Pending");
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
    public async Task RemoveFile_RejectsWhenSubmissionIsAccepted()
    {
        var submission = BuildSubmission();
        submission.Status = SubmissionStatus.Accepted;
        var file = BuildStoredFile();
        submission.Files.Add(file);
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        _fileStorageMock.Setup(s => s.DeleteAsync(It.IsAny<StoredFiles>())).Returns(Task.CompletedTask);

        var act = async () => await _service.RemoveFileAsync(file.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Pending*");
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
        file.Description = "test exhibit";
        submission.Files.Add(file);
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        var result = await _service.GetSubmissionsByFileNumberAsync("FILE001");

        var resultFile = result.First().Files.First();
        resultFile.MarkedValue.Should().Be("B");
        resultFile.EnteredValue.Should().Be("4");
        resultFile.Description.Should().Be("test exhibit");
        resultFile.Status.Should().Be("Entered");
    }

    // ── GetAcceptedPackage ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAcceptedPackage_ReturnsStream_WhenSubmissionAccepted()
    {
        var submission = BuildSubmission();
        submission.Status = SubmissionStatus.Accepted;
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        using var packageStream = new MemoryStream(new byte[] { 0x50, 0x4B });
        _fileStorageMock
            .Setup(s => s.GetAcceptedPackageAsync(It.IsAny<Submission>()))
            .ReturnsAsync(packageStream);

        var (stream, fileName, error) = await _service.GetAcceptedPackageAsync(submission.Id);

        stream.Should().NotBeNull();
        fileName.Should().Be($"submission-{submission.Id}-package.zip");
        error.Should().BeNull();
        _fileStorageMock.Verify(s => s.GetAcceptedPackageAsync(It.Is<Submission>(x => x.Id == submission.Id)), Times.Once);
    }

    [Fact]
    public async Task GetAcceptedPackage_ReturnsError_WhenSubmissionNotFound()
    {
        var (stream, fileName, error) = await _service.GetAcceptedPackageAsync(99999);

        stream.Should().BeNull();
        fileName.Should().BeNull();
        error.Should().Contain("not found");
        _fileStorageMock.Verify(s => s.GetAcceptedPackageAsync(It.IsAny<Submission>()), Times.Never);
    }

    [Theory]
    [InlineData(SubmissionStatus.Pending)]
    [InlineData(SubmissionStatus.Rejected)]
    public async Task GetAcceptedPackage_ReturnsError_WhenNotAccepted(SubmissionStatus status)
    {
        var submission = BuildSubmission();
        submission.Status = status;
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        var (stream, _, error) = await _service.GetAcceptedPackageAsync(submission.Id);

        stream.Should().BeNull();
        error.Should().Contain("Accepted");
        _fileStorageMock.Verify(s => s.GetAcceptedPackageAsync(It.IsAny<Submission>()), Times.Never);
    }

    [Fact]
    public async Task GetAcceptedPackage_ReturnsError_WhenPackageMissingOnDisk()
    {
        var submission = BuildSubmission();
        submission.Status = SubmissionStatus.Accepted;
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        _fileStorageMock
            .Setup(s => s.GetAcceptedPackageAsync(It.IsAny<Submission>()))
            .ThrowsAsync(new FileNotFoundException());

        var (stream, _, error) = await _service.GetAcceptedPackageAsync(submission.Id);

        stream.Should().BeNull();
        error.Should().Contain("not found");
    }
}
