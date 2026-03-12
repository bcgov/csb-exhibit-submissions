
using CES.Business.Models;
using CES.Entities;

namespace CES.Business.Interfaces
{
    public interface IFileStorage
    {
        Task<StoredFiles> SaveAsync(FileUpload file, string storagePath);
        Task<Stream> GetAsync(StoredFiles storedFile);
        Task DeleteAsync(StoredFiles storedFile);
        Task AcceptAsync(StoredFiles file);
    }
}