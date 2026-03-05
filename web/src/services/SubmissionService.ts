import type { ExhibitFormModel } from '@/models/ExhibitFormModel'
import httpClient from './httpClient'

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

      const apiReturn = await httpClient.post(url, formData, {
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

  return {
    submitExhibits,
  }
}
