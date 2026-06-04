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

    private static EvidenceSubmissionModel BuildModel(int fileCount = 2) => new()
    {
        ShortDate = "2026-01-01",
        AppearanceID = "APP001",
        FileNumberText = "FILE001",
        LocationId = "LOC001",
        RoomCode = "ROOM1",
        fileUploads = Enumerable.Range(0, fileCount).Select(i => new FileUpload
        {
            FileName = $"file{i}.mp4",
            ContentType = "video/mp4",
            Length = 1024,
            Location = "LOC001",
            Date = "2026-01-01",
            Room = "ROOM1",
            FileNumber = "FILE001",
            Content = new MemoryStream(new byte[] { 0x01, 0x02 })
        }).ToList()
    };

    private static StoredFiles BuildStoredFile(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        OriginalFileName = "file.mp4",
        StoredFileName = "stored.mp4",
        StoredPath = "LOC001/2026-01-01/ROOM1/FILE001",
        ContentType = "video/mp4",
        FileSize = 1024,
        StorageProvider = "Local"
    };

    [Fact]
    public async Task SubmitEvidence_PersistsSubmissionAndFiles()
    {
        var model = BuildModel(fileCount: 2);
        _fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<FileUpload>(), It.IsAny<string>()))
            .ReturnsAsync((FileUpload f, string _) => BuildStoredFile());

        await _service.SubmitEvidence(model);

        _db.Submissions.Count().Should().Be(1);
        _db.StoredFiles.Count().Should().Be(2);
    }

    [Fact]
    public async Task SubmitEvidence_CallsFileStorageSaveAsync()
    {
        var model = BuildModel(fileCount: 2);
        _fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<FileUpload>(), It.IsAny<string>()))
            .ReturnsAsync((FileUpload f, string _) => BuildStoredFile());

        await _service.SubmitEvidence(model);

        _fileStorageMock.Verify(
            s => s.SaveAsync(It.IsAny<FileUpload>(), It.IsAny<string>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RetrieveSubmission_ReturnsModel_WhenExists()
    {
        var file1 = BuildStoredFile();
        var file2 = BuildStoredFile();
        var submission = new Submission
        {
            AppearanceID = "APP001",
            FileNumberText = "FILE001",
            LocationId = "LOC001",
            LocationNameText = "Test Court",
            RoomCode = "ROOM1",
            Files = [file1, file2]
        };
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        var result = await _service.RetrieveSubmission(submission.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(submission.Id);
        result.FileNumber.Should().Be("FILE001");
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
            new Submission { AppearanceID = "A1", FileNumberText = "F1", LocationId = "L1", RoomCode = "R1" },
            new Submission { AppearanceID = "A2", FileNumberText = "F2", LocationId = "L2", RoomCode = "R2" },
            new Submission { AppearanceID = "A3", FileNumberText = "F3", LocationId = "L3", RoomCode = "R3", IsDeleted = true }
        );
        await _db.SaveChangesAsync();

        var result = await _service.RetrieveSubmissionListing();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task AcceptSubmissions_MarksFilesDeleted()
    {
        var fileId1 = Guid.NewGuid();
        var fileId2 = Guid.NewGuid();
        var file1 = BuildStoredFile(fileId1);
        var file2 = BuildStoredFile(fileId2);
        var submission = new Submission
        {
            AppearanceID = "APP001",
            FileNumberText = "FILE001",
            LocationId = "LOC001",
            RoomCode = "ROOM1",
            Files = [file1, file2]
        };
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
        var file1 = BuildStoredFile();
        var file2 = BuildStoredFile();
        var submission = new Submission
        {
            AppearanceID = "APP001",
            FileNumberText = "FILE001",
            LocationId = "LOC001",
            RoomCode = "ROOM1",
            Files = [file1, file2]
        };
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
}
