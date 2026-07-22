import router from '@/router';
import { useAuthStore } from '@/stores/authStore';
import api from './apiClient';

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
    const target =
      returnUrl && returnUrl !== '/' ? `?returnUrl=${encodeURIComponent(returnUrl)}` : '';
    window.location.assign(`/api/auth/login${target}`);
  };

  const logout = () => {
    const authStore = useAuthStore();
    authStore.clearAuth();
    router.push({ name: 'Login' });
  };

  const handleUnauthorized = (currentPath?: string) => {
    const authStore = useAuthStore();

    authStore.clearAuth();

    const query = currentPath && currentPath !== '/' ? { redirect: currentPath } : {};

    router.push({ name: 'Login', query });

    /* FUTURE KEYCLOAK IMPLEMENTATION:
      When switch to Keycloak, delete the router.push() above
      and replace it with something like:

      userManager.signinRedirect({ state: { redirectUrl: currentPath } });
    */
  };

  return {
    login,
    loginViaKeycloak,
    logout,
    handleUnauthorized,
  };
}
