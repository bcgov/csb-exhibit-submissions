namespace CES.API
{
    public interface IStorageOptions
    {
        public string Provider { get; }
        public string LocalPath { get; }
        public string AcceptedPath { get; }
        public long MaxFileSize { get; }
    }
    
    public class StorageOptions : IStorageOptions
    {
        public string Provider { get; set; } = "Local";
        public string LocalPath { get; set; } = "uploads";
        public string AcceptedPath { get; set; } = "accepted";
        public long MaxFileSize { get; set; } = 104857600; // 100MB
    }
}