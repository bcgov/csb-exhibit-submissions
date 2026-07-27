import { createPinia, setActivePinia } from 'pinia';

// Keep the bootstrap refresh off the network; a fresh load has no session here.
const bootstrap = vi.fn().mockResolvedValue(false);
vi.mock('@/services/sessionService', () => ({
  bootstrap,
  refresh: vi.fn(),
  scheduleRenewal: vi.fn(),
  default: { bootstrap, refresh: vi.fn(), scheduleRenewal: vi.fn() },
}));

import router from '@/router';

let assignMock: ReturnType<typeof vi.fn>;
let originalLocation: Location;

beforeEach(async () => {
  localStorage.clear();
  setActivePinia(createPinia());
  // Bypass off: the guard leaves the SPA for Keycloak instead of routing to /login.
  vi.stubEnv('VITE_DEV_AUTH_BYPASS', 'false');
  assignMock = vi.fn();
  originalLocation = window.location;
  vi.stubGlobal('location', { ...window.location, assign: assignMock });
  await router.push('/').catch(() => {});
});

afterEach(() => {
  vi.unstubAllEnvs();
  vi.stubGlobal('location', originalLocation);
});

describe('router guard with bypass off', () => {
  it('sends an unauthenticated visitor to the API login endpoint, not /login', async () => {
    await router.push('/admin/list').catch(() => {});

    expect(assignMock).toHaveBeenCalledTimes(1);
    expect(assignMock.mock.calls[0][0]).toContain('/api/auth/login');
    expect(assignMock.mock.calls[0][0]).toContain('returnUrl');
  });

  it('redirects a direct hit on /login through Keycloak', async () => {
    await router.push('/login').catch(() => {});

    expect(assignMock).toHaveBeenCalledTimes(1);
    expect(assignMock.mock.calls[0][0]).toContain('/api/auth/login');
  });

  it('still reaches the auth callback without a redirect', async () => {
    await router.push('/auth/callback?code=abc&state=xyz').catch(() => {});

    expect(router.currentRoute.value.name).toBe('AuthCallback');
    expect(assignMock).not.toHaveBeenCalled();
  });
});
