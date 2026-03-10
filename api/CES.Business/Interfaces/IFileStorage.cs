
using CES.Business.Models;
using CES.Entities;

namespace CES.Business.Interfaces
{
    public interface IFileStorage
    {
        Task<StoredFiles> SaveAsync(FileUpload file, string ticketNumber);
        Task<Stream> GetAsync(string storedFileName);
        Task DeleteAsync(string storedFileName);
        Task AcceptAsync(StoredFiles file, string ticketNumber);
    }
}