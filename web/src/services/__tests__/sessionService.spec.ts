import { createPinia, setActivePinia } from 'pinia';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/setup';
import { useAuthStore } from '@/stores/authStore';
import { refresh, scheduleRenewal, bootstrap } from '@/services/sessionService';
import { MIN_REFRESH_DELAY_MS, TOKEN_REFRESH_LEAD_MS } from '@/constants/auth';

vi.mock('@/router', () => ({
  default: { push: vi.fn(), replace: vi.fn(), currentRoute: { value: { path: '/' } } },
}));

/** Keycloak-shaped token: plural `roles`, expiring `secondsFromNow` out. */
function keycloakToken(secondsFromNow: number): string {
  const encode = (obj: unknown) => Buffer.from(JSON.stringify(obj)).toString('base64url');
  return `${encode({ alg: 'RS256', typ: 'JWT' })}.${encode({
    sub: 'sub-123',
    email: 'officer@gov.bc.ca',
    roles: ['ces-user'],
    exp: Math.floor(Date.now() / 1000) + secondsFromNow,
  })}.sig`;
}

beforeEach(() => {
  localStorage.clear();
  setActivePinia(createPinia());
});

describe('sessionService.refresh', () => {
  it('stores the renewed token and returns it', async () => {
    const token = keycloakToken(300);
    server.use(
      http.post('/api/auth/refresh', () =>
        HttpResponse.json({ accessToken: token, expiresIn: 300 }),
      ),
    );

    const returned = await refresh();

    expect(returned).toBe(token);
    expect(useAuthStore().token).toBe(token);
  });

  it('is single-flight: three concurrent calls issue exactly one request', async () => {
    let calls = 0;
    server.use(
      http.post('/api/auth/refresh', () => {
        calls += 1;
        return HttpResponse.json({ accessToken: keycloakToken(300), expiresIn: 300 });
      }),
    );

    await Promise.all([refresh(), refresh(), refresh()]);

    expect(calls).toBe(1);
  });

  it('allows a new request once the previous one has settled', async () => {
    let calls = 0;
    server.use(
      http.post('/api/auth/refresh', () => {
        calls += 1;
        return HttpResponse.json({ accessToken: keycloakToken(300), expiresIn: 300 });
      }),
    );

    await refresh();
    await refresh();

    expect(calls).toBe(2);
  });

  it('propagates a rejection when the session is over', async () => {
    server.use(http.post('/api/auth/refresh', () => HttpResponse.json({}, { status: 401 })));

    await expect(refresh()).rejects.toBeDefined();
  });
});

describe('sessionService.scheduleRenewal', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('schedules the renewal one lead-time before expiry', () => {
    const setTimeoutSpy = vi.spyOn(globalThis, 'setTimeout');
    const store = useAuthStore();
    store.setToken(keycloakToken(300)); // expiresAt ≈ now + 300s

    scheduleRenewal();

    const delay = setTimeoutSpy.mock.calls.at(-1)![1]!;
    // 300s out, renew 60s early → ~240s. Allow a small window for clock drift in the test.
    expect(delay).toBeGreaterThan(300_000 - TOKEN_REFRESH_LEAD_MS - 2_000);
    expect(delay).toBeLessThanOrEqual(300_000 - TOKEN_REFRESH_LEAD_MS);
    expect(store.renewalTimer).not.toBeNull();
  });

  it('floors the delay for a token already inside the lead window', () => {
    const setTimeoutSpy = vi.spyOn(globalThis, 'setTimeout');
    useAuthStore().setToken(keycloakToken(10)); // less than the 60s lead

    scheduleRenewal();

    expect(setTimeoutSpy.mock.calls.at(-1)![1]).toBe(MIN_REFRESH_DELAY_MS);
  });

  it('replaces a pending timer rather than stacking a second one', () => {
    const store = useAuthStore();
    store.setToken(keycloakToken(300));

    scheduleRenewal();
    const first = store.renewalTimer;
    scheduleRenewal();

    expect(store.renewalTimer).not.toBe(first);
  });
});

describe('sessionService.bootstrap', () => {
  it('returns true when a session is restored', async () => {
    server.use(
      http.post('/api/auth/refresh', () =>
        HttpResponse.json({ accessToken: keycloakToken(300), expiresIn: 300 }),
      ),
    );

    await expect(bootstrap()).resolves.toBe(true);
  });

  it('returns false — without throwing — when there is no live session', async () => {
    server.use(http.post('/api/auth/refresh', () => HttpResponse.json({}, { status: 401 })));

    await expect(bootstrap()).resolves.toBe(false);
  });
});
