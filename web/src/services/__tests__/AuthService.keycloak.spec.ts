import { createPinia, setActivePinia } from 'pinia';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/setup';
import { useAuthStore } from '@/stores/authStore';

vi.mock('@/router', () => ({
  default: { push: vi.fn(), replace: vi.fn(), currentRoute: { value: { path: '/' } } },
}));

import useAuthService from '@/services/AuthService';

let assignMock: ReturnType<typeof vi.fn>;
let originalLocation: Location;

beforeEach(() => {
  localStorage.clear();
  setActivePinia(createPinia());
  // Keycloak path: logout and unauthorized both leave the SPA for Keycloak.
  vi.stubEnv('VITE_DEV_AUTH_BYPASS', 'false');
  assignMock = vi.fn();
  originalLocation = window.location;
  vi.stubGlobal('location', { ...window.location, assign: assignMock, pathname: '/officer/court-list' });
});

afterEach(() => {
  vi.unstubAllEnvs();
  // Restore only location — unstubAllGlobals would tear down the shared localStorage stub.
  vi.stubGlobal('location', originalLocation);
});

describe('logoutViaKeycloak', () => {
  it('clears the store and navigates to the end-session URL', async () => {
    server.use(
      http.post('/api/auth/logout', () =>
        HttpResponse.json({ endSessionUrl: 'https://keycloak.test/logout' }),
      ),
    );
    const store = useAuthStore();
    store.setToken(buildToken());

    await useAuthService().logoutViaKeycloak();

    expect(store.token).toBeNull();
    expect(assignMock).toHaveBeenCalledWith('https://keycloak.test/logout');
  });

  it('still clears the store and leaves the app when logout fails', async () => {
    server.use(http.post('/api/auth/logout', () => HttpResponse.json({}, { status: 500 })));
    const store = useAuthStore();
    store.setToken(buildToken());

    await useAuthService().logoutViaKeycloak();

    // Never leave the user apparently signed in because logout errored.
    expect(store.token).toBeNull();
    expect(assignMock).toHaveBeenCalledWith('/');
  });
});

describe('handleUnauthorized on the Keycloak path', () => {
  it('redirects through the API login endpoint with the current path', () => {
    useAuthService().handleUnauthorized('/officer/court-list');

    expect(assignMock).toHaveBeenCalledWith('/api/auth/login?returnUrl=%2Fofficer%2Fcourt-list');
  });
});

function buildToken(): string {
  const encode = (obj: unknown) => Buffer.from(JSON.stringify(obj)).toString('base64url');
  return `${encode({ alg: 'RS256', typ: 'JWT' })}.${encode({
    sub: 'sub-123',
    email: 'officer@gov.bc.ca',
    roles: ['ces-user'],
    exp: Math.floor(Date.now() / 1000) + 300,
  })}.sig`;
}
