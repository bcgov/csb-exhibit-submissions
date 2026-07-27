import router from '@/router';
import { useAuthStore } from '@/stores/authStore';
import api from './apiClient';
import { isDevAuthBypass } from '@/constants/auth';
import { buildKeycloakLoginUrl } from '@/helpers/keycloakLogin';
import type { AuthLogoutResponse } from '@/models/AuthModels';

export default function useAuthService() {
  interface LoginResponse {
    token: string;
  }

  const login = async (username: string, password: string) => {
    try {
      const response = await api.post<LoginResponse>('/auth/login', {
        username,
        password,
      });

      const authStore = useAuthStore();
      authStore.setToken(response.data.token);
    } catch (error) {
      console.error('Authentication failed', error);
      throw error;
    }
  };

  /**
   * Starts the Keycloak flow. A full-page navigation, not an axios call: the browser
   * has to follow the API's 302 all the way to the IDIR login screen.
   */
  const loginViaKeycloak = (returnUrl?: string) => {
    window.location.assign(buildKeycloakLoginUrl(returnUrl));
  };

  /**
   * Ends the session in both CES and Keycloak. The API clears its cookie and returns the
   * RP-initiated logout URL; local state is dropped before navigating either way.
   */
  const logoutViaKeycloak = async () => {
    let endSessionUrl: string | null = null;
    try {
      const { data } = await api.post<AuthLogoutResponse>('/auth/logout');
      endSessionUrl = data.endSessionUrl;
    } catch (error) {
      // Never leave the user apparently signed in because logout failed.
      console.error('Keycloak logout failed', error);
    }

    useAuthStore().clearAuth();

    if (endSessionUrl) {
      window.location.assign(endSessionUrl);
    } else {
      window.location.assign('/');
    }
  };

  const logout = () => {
    if (!isDevAuthBypass()) {
      return logoutViaKeycloak();
    }

    const authStore = useAuthStore();
    authStore.clearAuth();
    router.push({ name: 'Login' });
  };

  const handleUnauthorized = (currentPath?: string) => {
    const authStore = useAuthStore();

    authStore.clearAuth();

    if (!isDevAuthBypass()) {
      // No mock login form to fall back to — go straight back through Keycloak.
      return loginViaKeycloak(currentPath);
    }

    const query = currentPath && currentPath !== '/' ? { redirect: currentPath } : {};

    router.push({ name: 'Login', query });
  };

  return {
    login,
    loginViaKeycloak,
    logout,
    logoutViaKeycloak,
    handleUnauthorized,
  };
}
