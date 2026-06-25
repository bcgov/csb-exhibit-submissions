namespace CES.Business.Models
{
    public class ExhibitMarkModel
    {
        public string MarkedValue { get; set; } = string.Empty;
    }

    public class ExhibitEnterModel
    {
        public string EnteredValue { get; set; } = string.Empty;
    }

    public class ExhibitDescriptionModel
    {
        public string Description { get; set; } = string.Empty;
    }

    public class ExhibitHistoryEntryModel
    {
        public string FieldName { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? ChangedBy { get; set; }
        public DateTime ChangedAtUTC { get; set; }
    }
}
