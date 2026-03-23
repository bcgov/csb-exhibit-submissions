import type { ExhibitFormModel } from '@/models/ExhibitFormModel'
import api from './apiClient'
import type { SubmissionReviewModel } from '@/models/SubmissionReviewModel'
import type { SubmissionAcceptanceModel } from '@/models/SubmissionAcceptanceModel'

export default function useSubmissionService() {
  const submitExhibits = async (
    model: ExhibitFormModel,
    files: File[],
    progressCallback?: (percent: number) => void,
  ): Promise<boolean> => {
    const url = `/submissions/submit/`
    let retVal: boolean = false

    try {
      const formData = new FormData()

      // Append text fields
      formData.append('date', model.date)
      formData.append('location', model.location)
      formData.append('room', model.room)
      formData.append('ticketNumber', model.ticketNumber)
      formData.append('disputantName', model.disputantName)
      formData.append('officerNumber', model.officerNumber)
      // Append files
      files.forEach((file) => {
        formData.append('files', file)
      })

      console.log('Done Preparing files:', formData)

      const apiReturn = await api.post(url, formData, {
        headers: {
          'Content-Type': 'multipart/form-data',
        },
        onUploadProgress: (event) => {
          const percent = Math.round((event.loaded * 100) / (event.total ?? 1))

          progressCallback?.(percent)
        },
      })

      //
      // });
      retVal = apiReturn.data ?? false
    } catch (err) {
      console.error(err)
    }

    return retVal
  }

  const retrieveSubmission = async (fileId: number): Promise<SubmissionReviewModel | undefined> => {
    const url = `/submissions/retrieve/`
    let apiReturn
    try {
      apiReturn = await api.get<SubmissionReviewModel>(url, {
        params: { fileId: fileId },
      })
    } catch (err) {
      console.error(err)
    }

    return apiReturn?.data
  }

  const retrieveSubmissionListing = async (): Promise<SubmissionReviewModel[] | undefined> => {
    const url = `/submissions/listing/`
    let apiReturn = undefined
    try {
      apiReturn = await api.get<SubmissionReviewModel[]>(url)
    } catch (err) {
      console.error(err)
      return undefined
    }

    return apiReturn?.data
  }

  const acceptSubmissionFiles = async (model: SubmissionAcceptanceModel): Promise<Boolean> => {

    const url = `/submissions/accept/`
    let retVal = false
    try {
      const apiReturn = await api.post(url, model)
      retVal = apiReturn.data ?? false
    } catch (err) {
      console.error(err)
    }
    return retVal
  }

  const rejectAndCloseSubmission = async (model: SubmissionAcceptanceModel): Promise<Boolean> => {

    const url = `/submissions/reject/`
    let retVal = false
    try {
      const apiReturn = await api.post(url, model)
      retVal = apiReturn.data ?? false
    } catch (err) {
      console.error(err)
    }
    return retVal
  }

  return {
    submitExhibits,
    retrieveSubmission,
    retrieveSubmissionListing,
    acceptSubmissionFiles,
    rejectAndCloseSubmission
  }
}
