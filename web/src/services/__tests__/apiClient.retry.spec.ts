import { createPinia, setActivePinia } from 'pinia';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/setup';

// vi.hoisted so the spy exists before the hoisted vi.mock factory runs.
const { handleUnauthorized } = vi.hoisted(() => ({ handleUnauthorized: vi.fn() }));

vi.mock('@/services/AuthService', () => ({
  default: () => ({
    handleUnauthorized,
    login: vi.fn(),
    loginViaKeycloak: vi.fn(),
    logout: vi.fn(),
  }),
}));

vi.mock('@/router', () => ({
  default: { push: vi.fn(), replace: vi.fn(), currentRoute: { value: { path: '/' } } },
}));

import api from '@/services/apiClient';

function keycloakToken(): string {
  const encode = (obj: unknown) => Buffer.from(JSON.stringify(obj)).toString('base64url');
  return `${encode({ alg: 'RS256', typ: 'JWT' })}.${encode({
    sub: 'sub-123',
    email: 'officer@gov.bc.ca',
    roles: ['ces-user'],
    exp: Math.floor(Date.now() / 1000) + 300,
  })}.sig`;
}

beforeEach(() => {
  localStorage.clear();
  setActivePinia(createPinia());
  handleUnauthorized.mockClear();
  // Keycloak path: the 401 retry only runs with bypass off. Read fresh per call.
  vi.stubEnv('VITE_DEV_AUTH_BYPASS', 'false');
});

afterEach(() => vi.unstubAllEnvs());

describe('apiClient 401 handling on the Keycloak path', () => {
  it('refreshes once and replays the request with the new token', async () => {
    let protectedCalls = 0;
    let refreshCalls = 0;
    const token = keycloakToken();

    server.use(
      http.get('/api/submissions/listing', ({ request }) => {
        protectedCalls += 1;
        if (protectedCalls === 1) return HttpResponse.json({}, { status: 401 });
        // The replay must carry the freshly minted bearer.
        return HttpResponse.json({ auth: request.headers.get('Authorization') });
      }),
      http.post('/api/auth/refresh', () => {
        refreshCalls += 1;
        return HttpResponse.json({ accessToken: token, expiresIn: 300 });
      }),
    );

    const response = await api.get('/submissions/listing');

    expect(refreshCalls).toBe(1);
    expect(protectedCalls).toBe(2);
    expect(response.data.auth).toBe(`Bearer ${token}`);
    expect(handleUnauthorized).not.toHaveBeenCalled();
  });

  it('does not loop when the replay also 401s', async () => {
    let refreshCalls = 0;

    server.use(
      http.get('/api/submissions/listing', () => HttpResponse.json({}, { status: 401 })),
      http.post('/api/auth/refresh', () => {
        refreshCalls += 1;
        return HttpResponse.json({ accessToken: keycloakToken(), expiresIn: 300 });
      }),
    );

    await expect(api.get('/submissions/listing')).rejects.toBeDefined();

    // One refresh, one replay, then hand off — never a second refresh.
    expect(refreshCalls).toBe(1);
    expect(handleUnauthorized).toHaveBeenCalledTimes(1);
  });

  it('never tries to refresh when the refresh endpoint itself 401s', async () => {
    let refreshCalls = 0;

    server.use(
      http.post('/api/auth/refresh', () => {
        refreshCalls += 1;
        return HttpResponse.json({}, { status: 401 });
      }),
    );

    await expect(api.post('/auth/refresh')).rejects.toBeDefined();

    // The request hit the endpoint once; the interceptor must not re-enter it.
    expect(refreshCalls).toBe(1);
    expect(handleUnauthorized).toHaveBeenCalledTimes(1);
  });
});
