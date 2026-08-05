// src/stores/useAuthStore.ts
import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { jwtDecode } from 'jwt-decode';
import type { JwtPayload, User } from '@/models/AuthModels';
import { KEYCLOAK_ROLE_MAP } from '@/constants/roles';
import { BYPASS_TOKEN_STORAGE_KEY, isDevAuthBypass, SECONDS_TO_MS } from '@/constants/auth';

export const useAuthStore = defineStore('auth', () => {
  // On the Keycloak path the access token lives in memory only. Losing it on reload is
  // intended: App.vue re-mints one from the HttpOnly cookie with no user-visible redirect.
  const token = ref<string | null>(
    isDevAuthBypass() ? localStorage.getItem(BYPASS_TOKEN_STORAGE_KEY) : null,
  );
  const user = ref<User | null>(null);
  const roles = ref<string[]>([]);
  /**
   * Profile-sourced, never from the token — IDIR exposes no officer-number claim, so this is
   * fetched from `GET /api/users/me` and held separately. Keeping it out of `user` is what
   * makes it survive a renewal: `decodeAndSetUser` rebuilds `user` from scratch every time.
   */
  const officerNumber = ref<string | null>(null);
  /** True once the profile fetch has settled, whether or not it found a number. */
  const profileLoaded = ref(false);
  /**
   * Single-flight guard for the profile fetch. Plain module-scope state rather than a ref —
   * nothing renders off it, and it must not be exposed as store state.
   */
  let profileRequest: Promise<void> | null = null;
  /** Epoch ms, derived from the token's `exp`. Drives the renewal timer. */
  const expiresAt = ref<number | null>(null);
  /** Handle for the pending renewal, owned by sessionService and cancelled here. */
  const renewalTimer = ref<ReturnType<typeof setTimeout> | null>(null);

  const isAuthenticated = computed(() => !!token.value && !isTokenExpired());
  const hasRole = (role: string) => roles.value.includes(role);
  const hasOfficerNumber = computed(() => !!officerNumber.value);

  function setToken(newToken: string) {
    token.value = newToken;
    // Persisted only on the bypass path, so the mock login survives a reload without an
    // API round-trip. The Keycloak token is never written to browser storage.
    if (isDevAuthBypass()) {
      localStorage.setItem(BYPASS_TOKEN_STORAGE_KEY, newToken);
    }
    decodeAndSetUser(newToken);
  }

  function decodeAndSetUser(jwt: string) {
    try {
      const decoded = jwtDecode<JwtPayload>(jwt);
      const decodedRoles = extractRoles(decoded);

      user.value = {
        id: decoded.sub,
        email: decoded.email,
        roles: decodedRoles,
        displayName: decoded.name ?? decoded.preferred_username,
        // Re-projected from the store ref, not the token: a renewal would otherwise drop it.
        officerNumber: officerNumber.value,
      };
      roles.value = decodedRoles;
      expiresAt.value = decoded.exp * SECONDS_TO_MS;
    } catch (error) {
      console.error('Invalid token format', error);
      clearAuth();
    }
  }

  /**
   * Mock tokens carry a singular `role` string; Keycloak tokens carry a plural `roles`
   * array of client role names that have to be mapped onto CES roles.
   */
  function extractRoles(decoded: JwtPayload): string[] {
    if (Array.isArray(decoded.roles)) {
      return decoded.roles
        .map((role) => KEYCLOAK_ROLE_MAP[role])
        .filter((role): role is string => !!role);
    }

    return decoded.role ? [decoded.role] : [];
  }

  function isTokenExpired(): boolean {
    if (!token.value) return true;
    if (expiresAt.value !== null) return expiresAt.value < Date.now();

    try {
      const decoded = jwtDecode<JwtPayload>(token.value);
      return decoded.exp * SECONDS_TO_MS < Date.now();
    } catch {
      return true;
    }
  }

  /**
   * Records a saved officer number, keeping the store ref and the projection on `user` in step.
   */
  function setOfficerNumber(value: string | null) {
    officerNumber.value = value;
    if (user.value) {
      user.value = { ...user.value, officerNumber: value };
    }
  }

  /**
   * Fetches the profile once per session. Single-flight, and a no-op once loaded, so the
   * renewal timer firing every few minutes does not re-fetch.
   * <p>
   * Failures are swallowed: a profile that cannot be read must not break an authenticated
   * session — the officer is simply prompted for their number again.
   */
  function loadProfile(): Promise<void> {
    if (profileLoaded.value) return Promise.resolve();

    profileRequest ??= (async () => {
      try {
        // Imported lazily to break the authStore ↔ apiClient module cycle.
        const { default: useUserService } = await import('@/services/UserService');
        const profile = await useUserService().getProfile();
        setOfficerNumber(profile?.officerNumber ?? null);
        profileLoaded.value = true;
      } catch (error) {
        console.warn('Could not load the user profile', error);
      } finally {
        profileRequest = null;
      }
    })();

    return profileRequest;
  }

  function clearAuth() {
    token.value = null;
    user.value = null;
    roles.value = [];
    expiresAt.value = null;
    officerNumber.value = null;
    profileLoaded.value = false;
    profileRequest = null;

    // Cancel any pending renewal, or a dead session keeps trying to refresh itself.
    if (renewalTimer.value !== null) {
      clearTimeout(renewalTimer.value);
      renewalTimer.value = null;
    }

    localStorage.removeItem(BYPASS_TOKEN_STORAGE_KEY);
  }

  // Initialize user state on load if a bypass token was restored above.
  if (token.value) {
    decodeAndSetUser(token.value);
  }

  return {
    token,
    user,
    isAuthenticated,
    roles,
    expiresAt,
    renewalTimer,
    officerNumber,
    profileLoaded,
    hasOfficerNumber,
    hasRole,
    setToken,
    setOfficerNumber,
    loadProfile,
    clearAuth,
  };
});
