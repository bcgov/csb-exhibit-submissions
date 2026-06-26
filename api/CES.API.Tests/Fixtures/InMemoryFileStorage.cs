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

    private readonly Dictionary<int, byte[]> _packages = new();

    public Task AcceptSubmissionAsync(Submission submission)
    {
        // Simulate package creation so it can be retrieved later.
        _packages[submission.Id] = System.Text.Encoding.UTF8.GetBytes($"package:{submission.Id}");
        return Task.CompletedTask;
    }

    public Task<Stream> GetAcceptedPackageAsync(Submission submission)
    {
        if (_packages.TryGetValue(submission.Id, out var bytes))
            return Task.FromResult<Stream>(new MemoryStream(bytes));

        throw new FileNotFoundException($"Accepted package for submission {submission.Id} not found");
    }
}
