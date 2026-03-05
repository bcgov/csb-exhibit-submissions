using CES.Entities.Infrastructure;

namespace CES.Entities
{
    public class Files:BaseEntity
    {
        public required Submission submission {get; set;}
        public string fileName {get;set;} = string.Empty;
    }
}