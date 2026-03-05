
using CES.Business.Models;
using CES.Entities;

namespace CES.Business.Interfaces
{
    public interface IFileStorage
    {
        Task<StoredFiles> SaveAsync(FileUpload file);
        Task<Stream> GetAsync(string storedFileName);
        Task DeleteAsync(string storedFileName);
    }
}