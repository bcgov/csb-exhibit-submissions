using CES.Business.Interfaces;
using CES.Business.Models;
using CES.Entities;

namespace CES.API.Tests.Fixtures;

public class InMemoryFileStorage : IFileStorage
{
    private readonly Dictionary<Guid, byte[]> _store = new();

    public Task<StoredFiles> SaveAsync(FileUpload file, string storagePath)
    {
        var id = Guid.NewGuid();
        using var ms = new MemoryStream();
        file.Content.CopyTo(ms);
        _store[id] = ms.ToArray();

        return Task.FromResult(new StoredFiles
        {
            Id = id,
            OriginalFileName = file.FileName,
            StoredFileName = $"{id}{Path.GetExtension(file.FileName)}",
            StoredPath = storagePath,
            ContentType = file.ContentType,
            FileSize = file.Length,
            StorageProvider = "InMemory"
        });
    }

    public Task<Stream> GetAsync(StoredFiles storedFile)
    {
        if (_store.TryGetValue(storedFile.Id, out var bytes))
            return Task.FromResult<Stream>(new MemoryStream(bytes));

        return Task.FromResult<Stream>(new MemoryStream(Array.Empty<byte>()));
    }

    public Task DeleteAsync(StoredFiles storedFile)
    {
        _store.Remove(storedFile.Id);
        return Task.CompletedTask;
    }

    private readonly Dictionary<Guid, byte[]> _accepted = new();

    public Task<AcceptedFileResult> PromoteToAcceptedAsync(Submission submission, StoredFiles file)
    {
        var ext = Path.GetExtension(file.OriginalFileName);
        var relativePath = $"{submission.LocationId}/{submission.RoomCode}/{submission.ShortDate}/{submission.Id}/{file.Id}{ext}";

        // Idempotent: copy the pending bytes into the accepted store only once.
        if (!_accepted.ContainsKey(file.Id))
            _accepted[file.Id] = _store.TryGetValue(file.Id, out var bytes) ? bytes : Array.Empty<byte>();

        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(_accepted[file.Id]));

        return Task.FromResult(new AcceptedFileResult
        {
            CanonicalPath = relativePath,
            AcceptedFileName = $"{file.Id}{ext}",
            Sha256 = hash,
        });
    }

    public Task WriteMetadataAsync(Submission submission, IReadOnlyList<SubmissionAuditLog> auditLogs)
    {
        // No-op for the in-memory store; metadata is a derived export.
        return Task.CompletedTask;
    }

    public Task<Stream> GetAcceptedExhibitAsync(StoredFiles file)
    {
        if (!file.IsAccepted || !_accepted.TryGetValue(file.Id, out var bytes))
            throw new FileNotFoundException($"Accepted exhibit {file.OriginalFileName} not found");

        return Task.FromResult<Stream>(new MemoryStream(bytes));
    }

    public Task<PendingCleanupResult> DeletePendingCopyAsync(StoredFiles file)
    {
        // Mirrors LocalFileStorage: the pending copy only goes away once the accepted
        // copy is present and its bytes match the hash recorded at acceptance.
        if (!file.IsAccepted || string.IsNullOrEmpty(file.Sha256) || !_accepted.TryGetValue(file.Id, out var accepted))
            return Task.FromResult(PendingCleanupResult.VerificationFailed);

        if (!_store.TryGetValue(file.Id, out var pending))
            return Task.FromResult(PendingCleanupResult.AlreadyRemoved);

        using var sha = System.Security.Cryptography.SHA256.Create();
        var acceptedHash = Convert.ToHexString(sha.ComputeHash(accepted));

        if (!string.Equals(acceptedHash, file.Sha256, StringComparison.OrdinalIgnoreCase)
            || !accepted.AsSpan().SequenceEqual(pending))
        {
            return Task.FromResult(PendingCleanupResult.VerificationFailed);
        }

        _store.Remove(file.Id);
        return Task.FromResult(PendingCleanupResult.Deleted);
    }
}
