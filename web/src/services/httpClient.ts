import axios from 'axios'
import type { AxiosInstance } from 'axios'

const baseURL = import.meta.env.VITE_API_URL

if (!baseURL) {
  throw new Error('VITE_API_URL is not defined')
}

const httpClient: AxiosInstance = axios.create({
  baseURL,
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json'
  }
})

// Optional: request interceptor
httpClient.interceptors.request.use((config) => {
  // Add auth token here later if needed
  return config
})

// Optional: response interceptor
httpClient.interceptors.response.use(
  (response) => response,
  (error) => {
    // Centralized error logging
    return Promise.reject(error)
  }
)

export default httpClient