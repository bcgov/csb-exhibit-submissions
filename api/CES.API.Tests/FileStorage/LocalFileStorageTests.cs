using System.Security.Cryptography;
using System.Text;
using CES.API;
using CES.API.FileStorage;
using CES.Business.FileStorage;
using CES.Entities;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace CES.API.Tests.FileStorage;

public class LocalFileStorageTests : IDisposable
{
    private readonly string _root;
    private readonly StorageOptions _options;
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"lfs-test-{Guid.NewGuid()}");
        _options = new StorageOptions
        {
            LocalPath = Path.Combine(_root, "uploads"),
            AcceptedPath = Path.Combine(_root, "accepted"),
            MaxFileSize = 104857600,
        };
        _storage = new LocalFileStorage(Options.Create(_options));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    // Writes a pending file to disk and returns its StoredFiles + submission.
    private (Submission submission, StoredFiles file) SeedPendingFile(byte[] content, Guid? id = null)
    {
        var submission = new Submission { Id = 7, LocationId = "LOC001", RoomCode = "ROOM1", ShortDate = "20260101" };
        var fileId = id ?? Guid.NewGuid();
        var storedPath = Path.Combine("LOC001", "20260101", "ROOM1", "7");
        var storedName = $"{fileId}.mp4";

        var dir = Path.Combine(_options.LocalPath, storedPath);
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, storedName), content);

        var file = new StoredFiles
        {
            Id = fileId,
            OriginalFileName = "evidence.mp4",
            StoredFileName = storedName,
            StoredPath = storedPath,
            ContentType = "video/mp4",
            FileSize = content.Length,
            SubmissionId = submission.Id,
            Submission = submission,
        };
        submission.Files.Add(file);
        return (submission, file);
    }

    private static string Sha256Hex(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }

    [Fact]
    public async Task PromoteToAccepted_CopiesBytesOnce_AndComputesSha256()
    {
        var content = Encoding.UTF8.GetBytes("exhibit bytes");
        var (submission, file) = SeedPendingFile(content);

        var result = await _storage.PromoteToAcceptedAsync(submission, file);

        result.Sha256.Should().Be(Sha256Hex(content));
        result.CanonicalPath.Should().Be($"loc001/room1/20260101/7/{file.Id}.mp4");

        var canonicalFull = Path.Combine(_options.AcceptedPath, result.CanonicalPath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(canonicalFull).Should().BeTrue();
        (await File.ReadAllBytesAsync(canonicalFull)).Should().Equal(content);
    }

    [Fact]
    public async Task PromoteToAccepted_IsIdempotent_SecondCallDoesNotThrowAndLeavesOneFile()
    {
        var content = Encoding.UTF8.GetBytes("exhibit bytes");
        var (submission, file) = SeedPendingFile(content);

        var first = await _storage.PromoteToAcceptedAsync(submission, file);
        var second = await _storage.PromoteToAcceptedAsync(submission, file);

        second.Sha256.Should().Be(first.Sha256);
        var folder = Path.Combine(_options.AcceptedPath, "loc001", "room1", "20260101", "7");
        Directory.GetFiles(folder).Should().ContainSingle();
        Directory.GetFiles(folder, "*.tmp").Should().BeEmpty();
    }

    [Fact]
    public async Task PromoteToAccepted_MultiTicketSubmission_StoresOnePhysicalFile()
    {
        // Single-instance is structural: an exhibit shared across N tickets lives in
        // its submission folder exactly once.
        var content = Encoding.UTF8.GetBytes("shared exhibit");
        var (submission, file) = SeedPendingFile(content);
        submission.Tickets.Add(new SubmissionTicket { AppearanceId = "A1", FileNumberText = "FILE001" });
        submission.Tickets.Add(new SubmissionTicket { AppearanceId = "A2", FileNumberText = "FILE002" });

        await _storage.PromoteToAcceptedAsync(submission, file);

        var folder = Path.Combine(_options.AcceptedPath, "loc001", "room1", "20260101", "7");
        Directory.GetFiles(folder).Should().ContainSingle();
    }

    [Fact]
    public async Task GetAcceptedExhibit_StreamsCanonicalBytes()
    {
        var content = Encoding.UTF8.GetBytes("canonical bytes");
        var (submission, file) = SeedPendingFile(content);
        var result = await _storage.PromoteToAcceptedAsync(submission, file);
        file.IsAccepted = true;
        file.CanonicalPath = result.CanonicalPath;

        await using var stream = await _storage.GetAcceptedExhibitAsync(file);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);

        ms.ToArray().Should().Equal(content);
    }

    [Fact]
    public async Task GetAcceptedExhibit_Throws_WhenNotAccepted()
    {
        var (_, file) = SeedPendingFile(Encoding.UTF8.GetBytes("x"));
        file.IsAccepted = false;

        var act = async () => await _storage.GetAcceptedExhibitAsync(file);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task GetAcceptedExhibit_Throws_WhenCanonicalFileMissing()
    {
        var (_, file) = SeedPendingFile(Encoding.UTF8.GetBytes("x"));
        file.IsAccepted = true;
        file.CanonicalPath = "loc001/room1/20260101/7/does-not-exist.mp4";

        var act = async () => await _storage.GetAcceptedExhibitAsync(file);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task WriteMetadata_ProducesOneMetadataFileInSubmissionFolder()
    {
        var content = Encoding.UTF8.GetBytes("bytes");
        var (submission, file) = SeedPendingFile(content);
        var result = await _storage.PromoteToAcceptedAsync(submission, file);
        file.IsAccepted = true;
        file.CanonicalPath = result.CanonicalPath;
        file.Sha256 = result.Sha256;

        await _storage.WriteMetadataAsync(submission, Array.Empty<SubmissionAuditLog>());

        var folder = Path.Combine(_options.AcceptedPath, "loc001", "room1", "20260101", "7");
        File.Exists(Path.Combine(folder, "metadata.json")).Should().BeTrue();
    }
}
