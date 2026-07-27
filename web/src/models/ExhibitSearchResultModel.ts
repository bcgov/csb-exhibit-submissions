import type { SubmissionFile } from './SubmissionReviewModel';

// One row per exhibit (mirrors the backend ExhibitSearchResultModel). Superset of
// ExhibitList.vue's PriorFileEntry ({ file, submissionDate?, fileNumbers[] }), so
// each result maps directly onto an entry.
export interface ExhibitSearchResultModel {
  file: SubmissionFile;
  submissionId: number;
  submissionDate?: string;
  appearanceDateTime?: string;
  location: string;
  room: string;
  fileNumbers: string[];
  accusedName?: string;
}

export interface ExhibitSearchFilter {
  fileNumberText?: string;
  accusedName?: string;
  appearanceDateFrom?: string;
  appearanceDateTo?: string;
}
