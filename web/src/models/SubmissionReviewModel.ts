import type { SubmissionTicketModel } from './ExhibitSubmissionModel';

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
  description?: string | null;
}

export interface ExhibitMarkModel {
  markedValue: string;
}

export interface ExhibitEnterModel {
  enteredValue: string;
}

export interface ExhibitDescriptionModel {
  description: string;
}

export interface SubmissionReviewModel {
  id: number;
  submissionDate?: string;
  courtDateTime: string;
  location: string;
  room: string;
  locationName: string;
  tickets: SubmissionTicketModel[];
  files: SubmissionFile[];
}
