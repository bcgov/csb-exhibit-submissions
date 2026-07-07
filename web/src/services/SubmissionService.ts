import type { ExhibitSubmissionModel } from '@/models/ExhibitSubmissionModel';
import type { PriorSubmissionModel } from '@/models/PriorSubmissionModel';
import type {
  ExhibitDescriptionModel,
  ExhibitEnterModel,
  ExhibitHistoryEntry,
  ExhibitMarkModel,
  PagedResult,
  SubmissionActionModel,
  SubmissionFile,
  SubmissionListFilter,
  SubmissionReviewModel,
} from '@/models/SubmissionReviewModel';
import api from './apiClient';

export default function useSubmissionService() {
  const submitExhibits = async (
    model: ExhibitSubmissionModel,
    files: File[],
    progressCallback?: (percent: number) => void,
    submissionId?: number | null,
  ): Promise<number | null> => {
    const url = `/submissions/submit/`;
    let retVal: number | null = null;

    try {
      const formData = new FormData();

      formData.append('shortDate', model.shortDate);
      formData.append('appearanceDateTime', model.appearanceDateTime);
      formData.append('locationId', model.locationId);
      formData.append('locationNameText', model.locationNameText);
      formData.append('roomCode', model.roomCode);
      formData.append('roomText', model.roomText);
      formData.append('officerNumber', model.officerNumber);

      // Append to the same submission when the officer stays on the page after a first upload.
      if (submissionId != null) {
        formData.append('submissionId', String(submissionId));
      }

      model.tickets.forEach((ticket, i) => {
        formData.append(`tickets[${i}].appearanceId`, ticket.appearanceId);
        formData.append(`tickets[${i}].appearanceDateTime`, ticket.appearanceDateTime);
        formData.append(`tickets[${i}].appearanceSequenceNumber`, ticket.appearanceSequenceNumber);
        formData.append(`tickets[${i}].appearanceReasonCode`, ticket.appearanceReasonCode);
        formData.append(`tickets[${i}].courtListType`, ticket.courtListType);
        formData.append(`tickets[${i}].fileNumberText`, ticket.fileNumberText);
        formData.append(`tickets[${i}].accusedName`, ticket.accusedName);
        formData.append(`tickets[${i}].accusedDOB`, ticket.accusedDOB);
      });

      files.forEach((file) => {
        formData.append('files', file);
      });

      const apiReturn = await api.post<{ submissionId: number }>(url, formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
        timeout: 0,
        onUploadProgress: (event) => {
          const percent = Math.round((event.loaded * 100) / (event.total ?? 1));
          progressCallback?.(percent);
        },
      });

      retVal = apiReturn.data?.submissionId ?? null;
    } catch (err) {
      console.error(err);
    }

    return retVal;
  };

  const retrieveSubmission = async (fileId: number): Promise<SubmissionReviewModel | undefined> => {
    const url = `/submissions/retrieve/`;
    const apiReturn = await api.get<SubmissionReviewModel>(url, {
      params: { fileId },
    });
    return apiReturn?.data;
  };

  const retrieveSubmissionListing = async (
    filter?: Partial<SubmissionListFilter>,
  ): Promise<PagedResult<SubmissionReviewModel> | undefined> => {
    const url = `/submissions/listing/`;
    const params: Record<string, unknown> = {};
    if (filter?.submissionDateFrom) params['submissionDateFrom'] = filter.submissionDateFrom;
    if (filter?.submissionDateTo) params['submissionDateTo'] = filter.submissionDateTo;
    if (filter?.fileNumberText) params['fileNumberText'] = filter.fileNumberText;
    if (filter?.accusedName) params['accusedName'] = filter.accusedName;
    if (filter?.status) params['status'] = filter.status;
    if (filter?.page) params['page'] = filter.page;
    if (filter?.pageSize) params['pageSize'] = filter.pageSize;
    const apiReturn = await api.get<PagedResult<SubmissionReviewModel>>(url, { params });
    return apiReturn?.data;
  };

  // Whole-submission Accept is retired (CES-39): a submission's Accepted status is
  // now derived from its files auto-accepting on classification, not set by a button.
  const rejectSubmission = async (model: SubmissionActionModel): Promise<boolean> => {
    try {
      await api.post(`/submissions/reject/`, model);
      return true;
    } catch {
      return false;
    }
  };

  const getSubmissionsByFileNumber = async (
    fileNumberText: string,
  ): Promise<PriorSubmissionModel[]> => {
    const url = `/submissions/by-file-number`;
    const apiReturn = await api.get<PriorSubmissionModel[]>(url, {
      params: { fileNumberText },
    });
    return apiReturn?.data ?? [];
  };

  const removeFile = async (fileId: string): Promise<boolean> => {
    try {
      await api.delete(`/submissions/files/${fileId}`);
      return true;
    } catch {
      return false;
    }
  };

  const markExhibit = async (fileId: string, model: ExhibitMarkModel): Promise<SubmissionFile> => {
    const result = await api.post<SubmissionFile>(`/files/${fileId}/mark`, model);
    return result.data;
  };

  const enterExhibit = async (
    fileId: string,
    model: ExhibitEnterModel,
  ): Promise<SubmissionFile> => {
    const result = await api.post<SubmissionFile>(`/files/${fileId}/enter`, model);
    return result.data;
  };

  const updateExhibitDescription = async (
    fileId: string,
    model: ExhibitDescriptionModel,
  ): Promise<SubmissionFile> => {
    const result = await api.patch<SubmissionFile>(`/files/${fileId}/description`, model);
    return result.data;
  };

  const getFileHistory = async (fileId: string): Promise<ExhibitHistoryEntry[]> => {
    const result = await api.get<ExhibitHistoryEntry[]>(`/files/${fileId}/history`);
    return result?.data ?? [];
  };

  return {
    submitExhibits,
    retrieveSubmission,
    retrieveSubmissionListing,
    rejectSubmission,
    getSubmissionsByFileNumber,
    removeFile,
    markExhibit,
    enterExhibit,
    updateExhibitDescription,
    getFileHistory,
  };
}
