import api from "./apiClient"

export default function useFileService() {
    
  const getFileBlob = async (fileId: string) => {
    const response = await api.get(`/files/${fileId}/view`, {
      responseType: 'blob'
    })

    return response.data
  }

  const downloadFile = async (fileId: string) => {
    const response = await api.get(`/files/${fileId}/download`, {
      responseType: 'blob'
    })

    return response.data
  }

  const getStreamUrl = async (fileId: string) => {
    const response = await api.get(`/files/${fileId}/stream-url`)
    return response.data.url
    }

  return {
    getFileBlob,
    downloadFile,
    getStreamUrl
  }
}