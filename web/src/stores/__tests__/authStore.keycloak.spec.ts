import { createPinia, setActivePinia } from 'pinia';
import { useAuthStore } from '@/stores/authStore';
import { BYPASS_TOKEN_STORAGE_KEY } from '@/constants/auth';

// The Keycloak path: token in memory, never localStorage. stubEnv is read fresh by
// isDevAuthBypass() on each call, so this holds regardless of module import order under
// the non-isolated pool.
beforeEach(() => vi.stubEnv('VITE_DEV_AUTH_BYPASS', 'false'));
afterEach(() => vi.unstubAllEnvs());

function keycloakToken(roles: string[], secondsFromNow = 300): string {
  const encode = (obj: unknown) => Buffer.from(JSON.stringify(obj)).toString('base64url');
  return `${encode({ alg: 'RS256', typ: 'JWT' })}.${encode({
    sub: 'sub-123',
    email: 'officer@gov.bc.ca',
    roles,
    exp: Math.floor(Date.now() / 1000) + secondsFromNow,
    name: 'Bryce Martel',
  })}.sig`;
}

beforeEach(() => {
  localStorage.clear();
  setActivePinia(createPinia());
});

describe('authStore on the Keycloak path', () => {
  it('does not write the access token to localStorage', () => {
    useAuthStore().setToken(keycloakToken(['ces-user']));

    expect(localStorage.getItem(BYPASS_TOKEN_STORAGE_KEY)).toBeNull();
  });

  it('maps Keycloak client roles onto CES roles', () => {
    const store = useAuthStore();
    store.setToken(keycloakToken(['ces-judicial', 'ces-user']));

    expect(store.roles).toEqual(['Admin', 'User']);
    expect(store.hasRole('Admin')).toBe(true);
  });

  it('drops unmapped Keycloak roles', () => {
    const store = useAuthStore();
    store.setToken(keycloakToken(['ces-user', 'account']));

    expect(store.roles).toEqual(['User']);
  });

  it('derives expiresAt from the token exp', () => {
    const store = useAuthStore();
    store.setToken(keycloakToken(['ces-user'], 300));

    const expected = Date.now() + 300_000;
    expect(store.expiresAt).toBeGreaterThan(expected - 2_000);
    expect(store.expiresAt).toBeLessThanOrEqual(expected);
  });

  it('surfaces the display name from the token', () => {
    const store = useAuthStore();
    store.setToken(keycloakToken(['ces-user']));

    expect(store.user?.displayName).toBe('Bryce Martel');
  });

  it('clearAuth cancels a pending renewal timer', () => {
    const store = useAuthStore();
    const handle = setTimeout(() => {}, 60_000);
    store.renewalTimer = handle;

    store.clearAuth();

    expect(store.renewalTimer).toBeNull();
  });
});
