// One description entry for an exhibit (CES-42). Append-only and immutable once saved:
// a correction or expansion is a new entry, and the earlier entries remain as history.
export interface ExhibitDescriptionModel {
  id: number;
  descriptionText: string;
  // Stored link to the author (ApplicationUser.Id); createdBy is the email the API
  // resolves from it for display, and is never persisted alongside the id.
  createdByUserId?: number | null;
  createdBy?: string | null;
  createdAtUTC: string;
}
