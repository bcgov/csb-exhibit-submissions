using CES.Entities;

namespace CES.Business.Extensions.Entities
{
    public static class StoredFilesExtensions
    {
        public static string DeriveStatus(this StoredFiles f)
        {
            if (f.IsDeleted) return "Removed";
            if (f.EnteredValue != null) return "Entered";
            if (f.MarkedValue != null) return "Marked";
            return "Unclassified";
        }
    }
}
