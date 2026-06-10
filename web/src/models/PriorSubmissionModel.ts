import type { SubmissionFile } from './SubmissionReviewModel';

export interface PriorSubmissionModel {
  submissionId: number;
  submissionDate?: string;
  appearanceDateTime?: string;
  location: string;
  room: string;
  files: SubmissionFile[];
}
