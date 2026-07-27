import { useAuthStore } from '@/stores/authStore';
import type { AxiosInstance, InternalAxiosRequestConfig } from 'axios';
import axios from 'axios';
import useAuthService from './AuthService';
import { isDevAuthBypass } from '@/constants/auth';

const baseURL = '/api';

/** Endpoints that must never trigger a renewal retry — they *are* the renewal path. */
const NO_RETRY_PATHS = ['/auth/refresh', '/auth/callback', '/auth/login', '/auth/logout'];

/** Axios config plus our one-shot retry marker. */
type RetriableRequest = InternalAxiosRequestConfig & { _retried?: boolean };

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
  async (error) => {
    if (axios.isAxiosError(error)) {
      const status = error.response?.status;
      const originalRequest = error.config as RetriableRequest | undefined;

      if (status === 401 && originalRequest && canRetry(originalRequest)) {
        // One attempt only: `_retried` is what stops an infinite refresh/retry cycle when
        // the API is rejecting tokens for a reason a refresh cannot fix.
        originalRequest._retried = true;
        try {
          // Imported lazily to break the apiClient ↔ sessionService module cycle.
          const { refresh } = await import('./sessionService');
          const newToken = await refresh();
          originalRequest.headers.Authorization = `Bearer ${newToken}`;
          return api(originalRequest);
        } catch {
          // The renewal itself failed, so the session is genuinely over.
          handleUnauthorized(window.location.pathname);
        }
      } else if (status === 401) {
        handleUnauthorized(window.location.pathname);
      }

      if (status === 403) {
        console.warn('Forbidden request', error.config?.url);
      }
    }

    return Promise.reject(error);
  },
);

function canRetry(request: RetriableRequest): boolean {
  // The bypass path has no refresh endpoint at all — a 401 there means straight back to
  // the mock login form.
  if (isDevAuthBypass() || request._retried) return false;

  return !NO_RETRY_PATHS.some((path) => request.url?.startsWith(path));
}

export default api;
