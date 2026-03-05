namespace CES.API
{
    public class StorageOptions : IStorageOptions
    {
        public string Provider { get; set; } = "Local";
        public string LocalPath { get; set; } = "uploads";
        public long MaxFileSize { get; set; } = 104857600; // 100MB
    }
}