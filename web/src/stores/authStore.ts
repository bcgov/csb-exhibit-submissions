// src/stores/useAuthStore.ts
import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { jwtDecode } from 'jwt-decode';
import type { JwtPayload, User } from '@/models/AuthModels'

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem('jwt_token'));
  const user = ref<User | null>(null);

  const isAuthenticated = computed(() => !!token.value && !isTokenExpired());

  function setToken(newToken: string) {
    token.value = newToken;
    localStorage.setItem('jwt_token', newToken);
    decodeAndSetUser(newToken);
  }

  function decodeAndSetUser(jwt: string) {
    try {
      const decoded = jwtDecode<JwtPayload>(jwt);
      user.value = {
        id: decoded.sub,
        email: decoded.email,
        roles: decoded.roles || [],
      };
    } catch (error) {
      console.error("Invalid token format", error);
      clearAuth();
    }
  }

  function isTokenExpired(): boolean {
    if (!token.value) return true;
    try {
      const decoded = jwtDecode<JwtPayload>(token.value);
      // exp is in seconds, Date.now() is in ms
      return decoded.exp * 1000 < Date.now(); 
    } catch {
      return true;
    }
  }

  function clearAuth() {
    token.value = null;
    user.value = null;
    localStorage.removeItem('jwt_token');
  }

  // Initialize user state on load if token exists
  if (token.value) {
    decodeAndSetUser(token.value);
  }

  return { token, user, isAuthenticated, setToken, clearAuth };
});