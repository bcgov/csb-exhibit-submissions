
export interface SubmissionFile {
  id: string
  originalFileName: string
  storedFileName: string
  viewUrl: string
  downloadUrl: string
  contentType: string
  fileSize: number
  storageProvider: string
}

export interface SubmissionReviewModel {
  id: number
  submissionDate?: string
  location: string
  room: string
  fileNumber: string
  accusedName: string
  officerNumber: string
  files: SubmissionFile[]
}
