import axios from 'axios';
import api from './apiClient';
import { useAuthStore } from '@/stores/authStore';
import type { AuthRefreshResponse } from '@/models/AuthModels';
import { MIN_REFRESH_DELAY_MS, TOKEN_REFRESH_LEAD_MS } from '@/constants/auth';

/**
 * Owns access-token renewal. The refresh token itself never appears here — it rides in
 * the HttpOnly `ces.session` cookie, which the browser attaches to `/api/auth` on its own.
 */

/**
 * Single-flight guard. Mandatory under refresh-token rotation: two parallel refreshes
 * would race, and the loser's rotated token would already have been invalidated.
 */
let inFlight: Promise<string> | null = null;

/**
 * Renews the access token, returning the new one. Concurrent callers await the same
 * request. Rejects when the Keycloak session is genuinely over.
 */
export async function refresh(): Promise<string> {
  inFlight ??= performRefresh().finally(() => {
    inFlight = null;
  });

  return inFlight;
}

async function performRefresh(): Promise<string> {
  const { data } = await api.post<AuthRefreshResponse>('/auth/refresh');

  const authStore = useAuthStore();
  authStore.setToken(data.accessToken);
  scheduleRenewal();

  return data.accessToken;
}

/**
 * Schedules the next renewal at `expiresAt − TOKEN_REFRESH_LEAD_MS`. Replaces any timer
 * already pending, so repeated calls cannot stack up.
 */
export function scheduleRenewal(): void {
  const authStore = useAuthStore();

  if (authStore.renewalTimer !== null) {
    clearTimeout(authStore.renewalTimer);
    authStore.renewalTimer = null;
  }

  if (authStore.expiresAt === null) return;

  const delay = Math.max(
    authStore.expiresAt - Date.now() - TOKEN_REFRESH_LEAD_MS,
    MIN_REFRESH_DELAY_MS,
  );

  authStore.renewalTimer = setTimeout(() => {
    // A failure on the proactive path is not fatal on its own: the response interceptor
    // gets a second attempt when the next request 401s.
    refresh().catch(() => {
      console.warn('Scheduled token renewal failed; the next request will retry.');
    });
  }, delay);
}

/**
 * Restores a session from the HttpOnly cookie on a hard reload. Returns false when there
 * is no live session, which is an ordinary state rather than an error — hence no redirect.
 */
export async function bootstrap(): Promise<boolean> {
  try {
    await refresh();
    return true;
  } catch (error) {
    // A 401 just means "not signed in"; anything else is worth surfacing in the console.
    if (!axios.isAxiosError(error) || error.response?.status !== 401) {
      console.warn('Session bootstrap failed', error);
    }
    return false;
  }
}

export default { refresh, scheduleRenewal, bootstrap };
