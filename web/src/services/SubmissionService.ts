import type { ExhibitFormModel } from '@/models/ExhibitFormModel'
import httpClient from './httpClient'

export default function useSubmissionService() {
  const submitExhibits = async (model: ExhibitFormModel): Promise<boolean> => {

    const url = `/submissions/submit/`
    let retVal: boolean = false

    try {
      const formData = new FormData();

      // Append text fields
      formData.append('date', model.date);
      formData.append('location', model.location);
      formData.append('room', model.room);
      formData.append('ticketNumber', model.ticketNumber);
      formData.append('disputantName', model.disputantName);
      formData.append('officerNumber', model.officerNumber);
      // Append files
      // files.value.forEach(file => {
      // formData.append('files', file)
      // })

        const apiReturn = await httpClient.post(url, formData, {
            headers: {
            'Content-Type': 'multipart/form-data',
            },
        });
        retVal = apiReturn.data ?? false;
    } catch (err) {
      console.error(err)
    }

    return retVal
  }

  return {
    submitExhibits,
  }
}
