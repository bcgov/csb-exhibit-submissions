using CES.Business.Services;
using CES.EF;
using CES.Entities;
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
}
