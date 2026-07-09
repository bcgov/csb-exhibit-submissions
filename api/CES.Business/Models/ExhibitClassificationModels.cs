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

    // Registry-only exhibit note (CES-38 extension). Read model returned to the client.
    public class ExhibitNoteModel
    {
        public int Id { get; set; }
        public string NoteText { get; set; } = string.Empty;
        public string? CreatedBy { get; set; }
        public DateTime CreatedAtUTC { get; set; }
    }

    // Request body for adding a note.
    public class AddExhibitNoteModel
    {
        public string NoteText { get; set; } = string.Empty;
    }
}
