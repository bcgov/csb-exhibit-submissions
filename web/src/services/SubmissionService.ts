import type { ExhibitSubmissionModel } from '@/models/ExhibitSubmissionModel'
import type { PriorSubmissionModel } from '@/models/PriorSubmissionModel'
import type { SubmissionAcceptanceModel } from '@/models/SubmissionAcceptanceModel'
import type { SubmissionReviewModel } from '@/models/SubmissionReviewModel'
import api from './apiClient'

export default function useSubmissionService() {
  const submitExhibits = async (
    model: ExhibitSubmissionModel,
    files: File[],
    progressCallback?: (percent: number) => void,
  ): Promise<boolean> => {
    const url = `/submissions/submit/`
    let retVal = false

    try {
      const formData = new FormData()

      // Shared submission fields
      formData.append('shortDate', model.shortDate)
      formData.append('locationId', model.locationId)
      formData.append('locationNameText', model.locationNameText)
      formData.append('roomCode', model.roomCode)
      formData.append('roomText', model.roomText)
      formData.append('officerNumber', model.officerNumber)

      // Indexed ticket fields: tickets[n].fieldName
      model.tickets.forEach((ticket, i) => {
        formData.append(`tickets[${i}].appearanceId`, ticket.appearanceId)
        formData.append(`tickets[${i}].appearanceDateTime`, ticket.appearanceDateTime)
        formData.append(`tickets[${i}].appearanceSequenceNumber`, ticket.appearanceSequenceNumber)
        formData.append(`tickets[${i}].appearanceReasonCode`, ticket.appearanceReasonCode)
        formData.append(`tickets[${i}].courtListType`, ticket.courtListType)
        formData.append(`tickets[${i}].fileNumberText`, ticket.fileNumberText)
        formData.append(`tickets[${i}].accusedName`, ticket.accusedName)
        formData.append(`tickets[${i}].accusedDOB`, ticket.accusedDOB)
      })

      // Only newly-selected files are uploaded; prior exhibits already live on the server.
      files.forEach((file) => {
        formData.append('files', file)
      })

      const apiReturn = await api.post(url, formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
        timeout: 0, // disabled to support large files
        onUploadProgress: (event) => {
          const percent = Math.round((event.loaded * 100) / (event.total ?? 1))
          progressCallback?.(percent)
        },
      })

      retVal = apiReturn.data ?? false
    } catch (err) {
      console.error(err)
    }

    return retVal
  }

  const retrieveSubmission = async (fileId: number): Promise<SubmissionReviewModel | undefined> => {
    const url = `/submissions/retrieve/`
    const apiReturn = await api.get<SubmissionReviewModel>(url, {
      params: { fileId },
    })
    return apiReturn?.data
  }

  const retrieveSubmissionListing = async (): Promise<SubmissionReviewModel[] | undefined> => {
    const url = `/submissions/listing/`
    const apiReturn = await api.get<SubmissionReviewModel[]>(url)
    return apiReturn?.data
  }

  const acceptSubmissionFiles = async (model: SubmissionAcceptanceModel): Promise<boolean> => {
    const url = `/submissions/accept/`
    const apiReturn = await api.post(url, model)
    return apiReturn.data ?? false
  }

  const rejectAndCloseSubmission = async (model: SubmissionAcceptanceModel): Promise<boolean> => {
    const url = `/submissions/reject/`
    const apiReturn = await api.post(url, model)
    return apiReturn.data ?? false
  }

  const getSubmissionsByFileNumber = async (fileNumberText: string): Promise<PriorSubmissionModel[]> => {
    const url = `/submissions/by-file-number`
    const apiReturn = await api.get<PriorSubmissionModel[]>(url, {
      params: { fileNumberText },
    })
    return apiReturn?.data ?? []
  }

  const removeFile = async (fileId: string): Promise<boolean> => {
    try {
      await api.delete(`/submissions/files/${fileId}`)
      return true
    } catch {
      return false
    }
  }

  return {
    submitExhibits,
    retrieveSubmission,
    retrieveSubmissionListing,
    acceptSubmissionFiles,
    rejectAndCloseSubmission,
    getSubmissionsByFileNumber,
    removeFile,
  }
}
