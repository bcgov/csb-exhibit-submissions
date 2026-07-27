import api from '@/services/apiClient';

describe('apiClient', () => {
  it('sends credentials so the HttpOnly auth cookies reach /api/auth', () => {
    // Without this the callback POST cannot carry ces.login, and the PKCE verifier
    // never reaches the exchange.
    expect(api.defaults.withCredentials).toBe(true);
  });

  it('is rooted at /api', () => {
    expect(api.defaults.baseURL).toBe('/api');
  });
});
