using System.Security.Cryptography;
using System.Text;
using CES.API;
using CES.API.FileStorage;
using CES.Business.FileStorage;
using CES.Business.Models;
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

    private string CanonicalFullPath(string canonicalPath)
        => Path.Combine(_options.AcceptedPath, canonicalPath.Replace('/', Path.DirectorySeparatorChar));

    private string PendingFullPath(StoredFiles file)
        => Path.Combine(_options.LocalPath, file.StoredPath, file.StoredFileName);

    // Promotes and records on the entity exactly what the DB holds after acceptance —
    // that record is what DeletePendingCopyAsync re-verifies against.
    private async Task<StoredFiles> AcceptAsync(Submission submission, StoredFiles file)
    {
        var result = await _storage.PromoteToAcceptedAsync(submission, file);
        file.IsAccepted = true;
        file.CanonicalPath = result.CanonicalPath;
        file.AcceptedFileName = result.AcceptedFileName;
        file.Sha256 = result.Sha256;
        return file;
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

    // ── Pending cleanup after acceptance ──────────────────────────────────
    // The pending original is the only surviving copy until the accepted one is
    // proven good, so every one of these asserts on which bytes are left on disk.

    [Fact]
    public async Task PromoteToAccepted_LeavesPendingCopyInPlace_AndNoTempFile()
    {
        // Promotion never deletes: removing the original is a separate step the
        // caller only takes once the acceptance is committed to the DB.
        var content = Encoding.UTF8.GetBytes("exhibit bytes");
        var (submission, file) = SeedPendingFile(content);

        await _storage.PromoteToAcceptedAsync(submission, file);

        File.Exists(PendingFullPath(file)).Should().BeTrue();
        var folder = Path.Combine(_options.AcceptedPath, "loc001", "room1", "20260101", "7");
        Directory.GetFiles(folder, "*.tmp").Should().BeEmpty();
    }

    [Fact]
    public async Task DeletePendingCopy_RemovesOriginal_WhenCanonicalVerifies()
    {
        var content = Encoding.UTF8.GetBytes("exhibit bytes");
        var (submission, file) = SeedPendingFile(content);
        await AcceptAsync(submission, file);

        var result = await _storage.DeletePendingCopyAsync(file);

        result.Should().Be(PendingCleanupResult.Deleted);
        File.Exists(PendingFullPath(file)).Should().BeFalse();

        // The exhibit survives the cleanup intact and still streams from canonical.
        (await File.ReadAllBytesAsync(CanonicalFullPath(file.CanonicalPath!))).Should().Equal(content);
        await using var stream = await _storage.GetAcceptedExhibitAsync(file);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        ms.ToArray().Should().Equal(content);
    }

    [Fact]
    public async Task DeletePendingCopy_ReturnsAlreadyRemoved_OnSecondCall()
    {
        var content = Encoding.UTF8.GetBytes("exhibit bytes");
        var (submission, file) = SeedPendingFile(content);
        await AcceptAsync(submission, file);

        await _storage.DeletePendingCopyAsync(file);
        var second = await _storage.DeletePendingCopyAsync(file);

        second.Should().Be(PendingCleanupResult.AlreadyRemoved);
        File.Exists(CanonicalFullPath(file.CanonicalPath!)).Should().BeTrue();
    }

    [Fact]
    public async Task DeletePendingCopy_KeepsOriginal_WhenCanonicalFileMissing()
    {
        var content = Encoding.UTF8.GetBytes("exhibit bytes");
        var (submission, file) = SeedPendingFile(content);
        await AcceptAsync(submission, file);
        File.Delete(CanonicalFullPath(file.CanonicalPath!));

        var result = await _storage.DeletePendingCopyAsync(file);

        result.Should().Be(PendingCleanupResult.VerificationFailed);
        (await File.ReadAllBytesAsync(PendingFullPath(file))).Should().Equal(content);
    }

    [Fact]
    public async Task DeletePendingCopy_KeepsOriginal_WhenCanonicalBytesTamperedWith()
    {
        var content = Encoding.UTF8.GetBytes("exhibit bytes");
        var (submission, file) = SeedPendingFile(content);
        await AcceptAsync(submission, file);

        // Same length, different bytes — only the hash catches this.
        await File.WriteAllBytesAsync(CanonicalFullPath(file.CanonicalPath!), Encoding.UTF8.GetBytes("EXHIBIT BYTES"));

        var result = await _storage.DeletePendingCopyAsync(file);

        result.Should().Be(PendingCleanupResult.VerificationFailed);
        (await File.ReadAllBytesAsync(PendingFullPath(file))).Should().Equal(content);
    }

    [Fact]
    public async Task DeletePendingCopy_KeepsOriginal_WhenCanonicalCopyIsTruncated()
    {
        var content = Encoding.UTF8.GetBytes("exhibit bytes");
        var (submission, file) = SeedPendingFile(content);
        await AcceptAsync(submission, file);

        await File.WriteAllBytesAsync(CanonicalFullPath(file.CanonicalPath!), content[..4]);

        var result = await _storage.DeletePendingCopyAsync(file);

        result.Should().Be(PendingCleanupResult.VerificationFailed);
        (await File.ReadAllBytesAsync(PendingFullPath(file))).Should().Equal(content);
    }

    [Fact]
    public async Task DeletePendingCopy_KeepsOriginal_WhenPendingBytesNoLongerMatchCanonical()
    {
        // The pending copy diverging from the accepted one means the two files are not
        // the same exhibit; deleting would destroy bytes nothing else holds.
        var content = Encoding.UTF8.GetBytes("exhibit bytes");
        var (submission, file) = SeedPendingFile(content);
        await AcceptAsync(submission, file);

        var replacement = Encoding.UTF8.GetBytes("EXHIBIT BYTES");
        await File.WriteAllBytesAsync(PendingFullPath(file), replacement);

        var result = await _storage.DeletePendingCopyAsync(file);

        result.Should().Be(PendingCleanupResult.VerificationFailed);
        (await File.ReadAllBytesAsync(PendingFullPath(file))).Should().Equal(replacement);
    }

    [Fact]
    public async Task DeletePendingCopy_KeepsOriginal_WhenFileIsNotAccepted()
    {
        var content = Encoding.UTF8.GetBytes("exhibit bytes");
        var (_, file) = SeedPendingFile(content);

        var result = await _storage.DeletePendingCopyAsync(file);

        result.Should().Be(PendingCleanupResult.VerificationFailed);
        File.Exists(PendingFullPath(file)).Should().BeTrue();
    }

    [Fact]
    public async Task DeletePendingCopy_KeepsOriginal_WhenNoHashWasRecorded()
    {
        // Flagged accepted but with no hash to verify against — there is nothing to
        // prove the canonical copy is the right one, so nothing gets deleted.
        var content = Encoding.UTF8.GetBytes("exhibit bytes");
        var (submission, file) = SeedPendingFile(content);
        await AcceptAsync(submission, file);
        file.Sha256 = null;

        var result = await _storage.DeletePendingCopyAsync(file);

        result.Should().Be(PendingCleanupResult.VerificationFailed);
        File.Exists(PendingFullPath(file)).Should().BeTrue();
    }
}
