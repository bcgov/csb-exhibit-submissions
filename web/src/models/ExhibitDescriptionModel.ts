// One description entry for an exhibit (CES-42). Append-only and immutable once saved:
// a correction or expansion is a new entry, and the earlier entries remain as history.
export interface ExhibitDescriptionModel {
  id: number;
  descriptionText: string;
  createdBy?: string | null;
  createdAtUTC: string;
}
