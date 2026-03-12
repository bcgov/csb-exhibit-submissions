using CES.Entities.Infrastructure;

namespace CES.Entities
{
    public class StoredFiles
    {
        public Guid Id { get; set; } = new Guid();
        public string OriginalFileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
        public string StoredPath { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string StorageProvider { get; set; } = string.Empty;
        public DateTime CreatedDateUTC { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedDateUTC { get; set; }
        public bool IsDeleted { get; set; } = false;

        public StoredFiles()
        {
            CreatedDateUTC = SystemDate.UtcNow();
        }

        public void SetUpdateBy(string updator = "System")
        {
            UpdatedBy = updator;
            UpdatedDateUTC = SystemDate.UtcNow();
        }
        
    }
}