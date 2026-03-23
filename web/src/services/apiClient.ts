import { useAuthStore } from '@/stores/authStore'
import axios from 'axios'
import type { AxiosInstance } from 'axios'
import useAuthService from './AuthService'

const baseURL = import.meta.env.VITE_API_URL

if (!baseURL) {
  throw new Error('VITE_API_URL is not defined')
}

const api: AxiosInstance = axios.create({
  baseURL,
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json'
  }
})

const { handleUnauthorized } = useAuthService();

api.interceptors.request.use((config) => {
    const authStore = useAuthStore();
    if (authStore.token) {
      config.headers.Authorization = `Bearer ${authStore.token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
)

api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      // Grab the URL the user is currently on so we can send them back later
      const currentPath = window.location.pathname;
      
      // Trigger our abstracted redirect logic
      handleUnauthorized(currentPath);
    }

    // Still reject the promise so the individual component knows the call failed
    return Promise.reject(error);
  }
)

export default api