import { useAuthStore } from '@/stores/authStore';
import type { AxiosInstance } from 'axios';
import axios from 'axios';
import useAuthService from './AuthService';

const baseURL = '/api';

const api: AxiosInstance = axios.create({
  baseURL,
  // timeout: 60000,
  // Required so the HttpOnly auth cookies (ces.login / ces.session) are sent to the
  // /api/auth endpoints. They are Path-scoped, so no other request carries them.
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json',
  },
});

const { handleUnauthorized } = useAuthService();

api.interceptors.request.use(
  (config) => {
    const authStore = useAuthStore();
    if (authStore.token) {
      config.headers.Authorization = `Bearer ${authStore.token}`;
    }
    return config;
  },
  (error) => Promise.reject(error),
);

api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (axios.isAxiosError(error)) {
      const status = error.response?.status;
      const currentPath = window.location.pathname;

      if (status === 401) {
        handleUnauthorized(currentPath); // login redirect
      }

      if (status === 403) {
        console.warn('Forbidden request', error.config?.url);
      }
    }

    return Promise.reject(error);
  },
);

export default api;
