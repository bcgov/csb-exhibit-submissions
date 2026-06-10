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
