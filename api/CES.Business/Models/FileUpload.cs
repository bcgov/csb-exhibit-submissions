
namespace CES.Business.Models
{
    public class FileUpload
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Length { get; set; }
        public required string Location { get; set; }
        public required string Date { get; set; }
        public required string Room { get; set; }
        public Stream Content { get; set; } = Stream.Null;
    }
}
