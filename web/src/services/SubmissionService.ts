import { localDateToUtc } from '@/helpers/formatters'
import type { ExhibitSubmissionModel } from '@/models/ExhibitSubmissionModel'
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
    let retVal: boolean = false

    try {
      const formData = new FormData()
      const date = localDateToUtc(model.appearanceDateTime) ?? ''
      console.log(model.appearanceDateTime, date, 'appearance datetime')
      // Append text fields
      formData.append('appearanceID', model.appearanceId)
      formData.append('appearanceDateTime', model.appearanceDateTime)
      formData.append('shortDate', model.shortDate)
      formData.append('appearanceSequenceNumber', model.appearanceSequenceNumber)
      formData.append('appearanceReasonCode', model.appearanceReasonCode)
      formData.append('courtListType', model.courtListType)
      formData.append('fileNumberText', model.fileNumberText)
      formData.append('locationId', model.locationId)
      formData.append('locationNameText', model.locationNameText)
      formData.append('roomCode', model.roomCode)
      formData.append('roomText', model.roomText)
      formData.append('accusedName', model.accusedName)
      formData.append('accusedDOB', model.accusedDOB)
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
        timeout: 0, //disabled timeouts to support larger files
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
    const apiReturn = await api.get<SubmissionReviewModel>(url, {
      params: { fileId: fileId },
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
    let retVal = false
    const apiReturn = await api.post(url, model)
    retVal = apiReturn.data ?? false
    return retVal
  }

  const rejectAndCloseSubmission = async (model: SubmissionAcceptanceModel): Promise<boolean> => {
    const url = `/submissions/reject/`
    let retVal = false
    const apiReturn = await api.post(url, model)
    retVal = apiReturn.data ?? false
    return retVal
  }

  return {
    submitExhibits,
    retrieveSubmission,
    retrieveSubmissionListing,
    acceptSubmissionFiles,
    rejectAndCloseSubmission,
  }
}
