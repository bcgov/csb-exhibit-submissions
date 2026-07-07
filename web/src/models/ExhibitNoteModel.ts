// Registry-only exhibit note (CES-38 extension). Append-only and immutable once saved.
export interface ExhibitNoteModel {
  id: number;
  noteText: string;
  createdBy?: string | null;
  createdAtUTC: string;
}
