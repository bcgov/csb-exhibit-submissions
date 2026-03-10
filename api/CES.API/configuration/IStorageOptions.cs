namespace CES.API
{
    public interface IStorageOptions
    {
        public string Provider { get; }
        public string LocalPath { get; }
        public string AcceptedPath { get; }
        public long MaxFileSize { get; }
    }
}