using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Business.Services;
using CES.EF;
using CES.Entities;
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
    public async Task RetrieveSubmissionListing_ExcludesDeleted()
    {
        _db.Submissions.AddRange(
            BuildSubmission("F1"),
            BuildSubmission("F2"),
            new Submission { LocationId = "L3", RoomCode = "R3", IsDeleted = true }
        );
        await _db.SaveChangesAsync();

        var result = await _service.RetrieveSubmissionListing();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task RetrieveSubmissionListing_IncludesTicketsInResponse()
    {
        var sub = BuildSubmission("FILE001");
        _db.Submissions.Add(sub);
        await _db.SaveChangesAsync();

        var result = await _service.RetrieveSubmissionListing();

        result.First().Tickets.Should().HaveCount(1);
        result.First().Tickets[0].FileNumberText.Should().Be("FILE001");
    }

    [Fact]
    public async Task AcceptSubmissions_MarksFilesDeleted()
    {
        var fileId1 = Guid.NewGuid();
        var fileId2 = Guid.NewGuid();
        var submission = BuildSubmission();
        submission.Files.Add(BuildStoredFile(fileId1));
        submission.Files.Add(BuildStoredFile(fileId2));
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        _fileStorageMock
            .Setup(s => s.AcceptAsync(It.IsAny<StoredFiles>()))
            .Returns(Task.CompletedTask);

        var model = new EvidenceAcceptanceModel
        {
            FileId = submission.Id,
            acceptedFiles = [fileId1, fileId2]
        };

        var result = await _service.AcceptSubmissions(model);

        result.Should().BeTrue();
        _db.StoredFiles.Where(f => f.Id == fileId1 || f.Id == fileId2)
            .All(f => f.IsDeleted).Should().BeTrue();
    }

    [Fact]
    public async Task RejectSubmissions_DeletesSubmissionAndFiles()
    {
        var submission = BuildSubmission();
        submission.Files.Add(BuildStoredFile());
        submission.Files.Add(BuildStoredFile());
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        _fileStorageMock
            .Setup(s => s.DeleteAsync(It.IsAny<StoredFiles>()))
            .Returns(Task.CompletedTask);

        var model = new EvidenceAcceptanceModel
        {
            FileId = submission.Id,
            acceptedFiles = []
        };

        var result = await _service.RejectSubmissions(model);

        result.Should().BeTrue();
        _db.Submissions.Find(submission.Id)!.IsDeleted.Should().BeTrue();
        _fileStorageMock.Verify(s => s.DeleteAsync(It.IsAny<StoredFiles>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetSubmissionsByFileNumber_ReturnsMatchingSubmissions()
    {
        var sub1 = BuildSubmission("FILE001");
        var sub2 = BuildSubmission("FILE001");
        sub2.LocationId = "LOC002"; // different location — still returned
        var sub3 = BuildSubmission("FILE999"); // different file number
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
}
