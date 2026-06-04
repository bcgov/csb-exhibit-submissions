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

    public Task AcceptAsync(StoredFiles file)
    {
        return Task.CompletedTask;
    }
}
