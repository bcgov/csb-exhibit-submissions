// Registry-only exhibit note (CES-38 extension). Append-only and immutable once saved.
export interface ExhibitNoteModel {
  id: number;
  noteText: string;
  // See ExhibitDescriptionModel — id is the stored link, createdBy the resolved email.
  createdByUserId?: number | null;
  createdBy?: string | null;
  createdAtUTC: string;
}
