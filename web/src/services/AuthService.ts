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
    logout,
    handleUnauthorized,
  };
}
