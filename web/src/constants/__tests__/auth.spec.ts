import { isDevAuthBypass } from '@/constants/auth';

describe('auth constants', () => {
  afterEach(() => {
    delete window.__CES_CONFIG__;
    vi.unstubAllEnvs();
  });

  it('uses runtime config before Vite env when present', () => {
    vi.stubEnv('VITE_DEV_AUTH_BYPASS', 'true');
    window.__CES_CONFIG__ = { VITE_DEV_AUTH_BYPASS: 'false' };

    expect(isDevAuthBypass()).toBe(false);
  });

  it('falls back to Vite env when runtime config has not been replaced', () => {
    vi.stubEnv('VITE_DEV_AUTH_BYPASS', 'false');
    window.__CES_CONFIG__ = { VITE_DEV_AUTH_BYPASS: '__CES_VITE_DEV_AUTH_BYPASS__' };

    expect(isDevAuthBypass()).toBe(false);
  });
});
