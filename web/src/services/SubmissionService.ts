import type { ExhibitFormModel } from '@/models/ExhibitFormModel'
import api from './apiClient'
import type { SubmissionReviewModel } from '@/models/SubmissionReviewModel'
import type { SubmissionAcceptanceModel } from '@/models/SubmissionAcceptanceModel'
import type { CourtFileList } from '@/models/CourtFileList'
import type { ExhibitSubmissionModel } from '@/models/ExhibitSubmissionModel'
import { localDateToUtc } from '@/helpers/formatters'

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
      const date = localDateToUtc(model.appearanceDateTime) ?? ""
      // Append text fields
      formData.append('appearanceID', model.appearanceId)
      formData.append('appearanceDateTime', date)
      formData.append('courtListType', model.courtListType)
      formData.append('FileNumberText', model.fileNumberText)
      formData.append('LocationId', model.locationId)
      formData.append('LocationNameText', model.locationNameText)
      formData.append('RoomCode', model.roomCode)
      formData.append('RoomText', model.roomText)
      formData.append('AccusedName', model.accusedName)
      formData.append('AccusedDOB', model.accusedDOB)
      formData.append('OfficerNumber', model.officerNumber)
      
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
    let apiReturn = await api.get<SubmissionReviewModel>(url, {
        params: { fileId: fileId },
      })

    return apiReturn?.data
  }

  const retrieveSubmissionListing = async (): Promise<SubmissionReviewModel[] | undefined> => {
    const url = `/submissions/listing/`
    const apiReturn = await api.get<SubmissionReviewModel[]>(url)

    return apiReturn?.data
  }

  const acceptSubmissionFiles = async (model: SubmissionAcceptanceModel): Promise<Boolean> => {

    const url = `/submissions/accept/`
    let retVal = false
    const apiReturn = await api.post(url, model)
    retVal = apiReturn.data ?? false
    return retVal
  }

  const rejectAndCloseSubmission = async (model: SubmissionAcceptanceModel): Promise<Boolean> => {

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
    rejectAndCloseSubmission
  }
}
