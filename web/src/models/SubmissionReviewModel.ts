
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
  date?: string
  location: string
  room: string
  ticketNumber: string
  disputantName: string
  officerNumber: string
  files: SubmissionFile[]
}
