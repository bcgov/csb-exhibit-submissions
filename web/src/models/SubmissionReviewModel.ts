import type { ExhibitDescriptionModel } from './ExhibitDescriptionModel';
import type { SubmissionTicketModel } from './ExhibitSubmissionModel';

export type SubmissionStatus = 'Pending' | 'Accepted' | 'Rejected';

export interface SubmissionFile {
  id: string;
  originalFileName: string;
  storedFileName: string;
  viewUrl: string;
  downloadUrl: string;
  contentType: string;
  fileSize: number;
  storageProvider: string;
  status?: string;
  markedValue?: string | null;
  markedAt?: string | null;
  enteredValue?: string | null;
  enteredAt?: string | null;
  // Append-only description entries, oldest → newest (CES-42).
  descriptions: ExhibitDescriptionModel[];
  evidenceSourceType?: string | null;
  deletedAt?: string | null;
}

export interface ExhibitMarkModel {
  markedValue: string;
}

export interface ExhibitEnterModel {
  enteredValue: string;
}

export interface ExhibitEvidenceSourceModel {
  evidenceSourceType: string;
}

export interface ExhibitHistoryEntry {
  fieldName: string;
  oldValue?: string | null;
  newValue?: string | null;
  // See ExhibitDescriptionModel — id is the stored link, changedBy the resolved email.
  changedByUserId?: number | null;
  changedBy?: string | null;
  changedAtUTC: string;
}

export interface SubmissionReviewModel {
  id: number;
  submissionDate?: string;
  courtDateTime: string;
  location: string;
  room: string;
  locationName: string;
  status: SubmissionStatus;
  statusChangedDate?: string | null;
  exhibitCount: number;
  tickets: SubmissionTicketModel[];
  files: SubmissionFile[];
}

export interface SubmissionListFilter {
  submissionDateFrom?: string;
  submissionDateTo?: string;
  fileNumberText?: string;
  accusedName?: string;
  status?: SubmissionStatus | '';
  page: number;
  pageSize: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface SubmissionActionModel {
  submissionId: number;
}
