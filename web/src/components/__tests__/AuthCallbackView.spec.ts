import { mount, flushPromises } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/setup';
import AuthCallbackView from '@/views/AuthCallbackView.vue';
import { useAuthStore } from '@/stores/authStore';

const { replaceMock, routeQuery } = vi.hoisted(() => ({
  replaceMock: vi.fn(),
  routeQuery: { value: {} as Record<string, string> },
}));

vi.mock('@/router', () => ({
  default: {
    push: vi.fn(),
    replace: replaceMock,
    currentRoute: { value: { path: '/auth/callback', query: {} } },
  },
}));

vi.mock('vue-router', async (importOriginal) => {
  const actual = await importOriginal<typeof import('vue-router')>();
  return {
    ...actual,
    useRouter: () => ({ replace: replaceMock, push: vi.fn() }),
    useRoute: () => ({ query: routeQuery.value }),
  };
});

/** Keycloak-shaped access token: plural `roles`, unlike the mock token's singular `role`. */
function buildKeycloakToken(): string {
  const encode = (obj: unknown) => Buffer.from(JSON.stringify(obj)).toString('base64url');
  return `${encode({ alg: 'RS256', typ: 'JWT' })}.${encode({
    sub: 'a1b2c3d4-0000-0000-0000-000000000000',
    email: 'officer@gov.bc.ca',
    role: 'User',
    exp: Math.floor(Date.now() / 1000) + 300,
  })}.sig`;
}

function mountCallback() {
  return mount(AuthCallbackView, { global: { plugins: [createPinia()] } });
}

beforeEach(() => {
  localStorage.clear();
  setActivePinia(createPinia());
  replaceMock.mockClear();
  routeQuery.value = {};
});

describe('AuthCallbackView', () => {
  it('posts the code and state, stores the token, and lands on returnUrl', async () => {
    const token = buildKeycloakToken();
    let received: { code?: string; state?: string } = {};

    server.use(
      http.post('/api/auth/callback', async ({ request }) => {
        received = (await request.json()) as { code: string; state: string };
        return HttpResponse.json({
          accessToken: token,
          expiresIn: 300,
          returnUrl: '/officer/court-list',
        });
      }),
    );

    routeQuery.value = { code: 'the-code', state: 'the-state' };
    mountCallback();
    await flushPromises();

    expect(received.code).toBe('the-code');
    expect(received.state).toBe('the-state');
    expect(useAuthStore().token).toBe(token);
    expect(replaceMock).toHaveBeenLastCalledWith('/officer/court-list');
  });

  it('clears the authorization code from the URL before posting it', async () => {
    let queryClearedFirst = false;

    server.use(
      http.post('/api/auth/callback', () => {
        // By the time the exchange is in flight, the code must already be out of
        // history — this is what stops a single-use code persisting on the Back button.
        queryClearedFirst = replaceMock.mock.calls.some(
          ([arg]) => typeof arg === 'object' && arg?.path === '/auth/callback',
        );
        return HttpResponse.json({ accessToken: buildKeycloakToken(), expiresIn: 300, returnUrl: '/' });
      }),
    );

    routeQuery.value = { code: 'the-code', state: 'the-state' };
    mountCallback();
    await flushPromises();

    expect(queryClearedFirst).toBe(true);
  });

  it('routes a cancelled sign-in to the error view with the reason', async () => {
    // Keycloak reports failures by redirecting with ?error=, not with an error status.
    routeQuery.value = {
      error: 'access_denied',
      error_description: 'User cancelled sign-in',
    };
    mountCallback();
    await flushPromises();

    expect(replaceMock).toHaveBeenCalledWith({
      name: 'AuthError',
      query: { reason: 'User cancelled sign-in' },
    });
  });

  it('falls back to the raw error code when no description is supplied', async () => {
    routeQuery.value = { error: 'invalid_request' };
    mountCallback();
    await flushPromises();

    expect(replaceMock).toHaveBeenCalledWith({
      name: 'AuthError',
      query: { reason: 'invalid_request' },
    });
  });

  it('routes to the error view when the code is missing', async () => {
    routeQuery.value = { state: 'the-state' };
    mountCallback();
    await flushPromises();

    expect(replaceMock).toHaveBeenCalledWith({ name: 'AuthError' });
  });

  it('routes to the error view when the state is missing', async () => {
    routeQuery.value = { code: 'the-code' };
    mountCallback();
    await flushPromises();

    expect(replaceMock).toHaveBeenCalledWith({ name: 'AuthError' });
  });

  it('routes to the error view when the exchange is rejected', async () => {
    server.use(http.post('/api/auth/callback', () => HttpResponse.json({}, { status: 400 })));

    routeQuery.value = { code: 'stale-code', state: 'the-state' };
    mountCallback();
    await flushPromises();

    expect(replaceMock).toHaveBeenLastCalledWith({ name: 'AuthError' });
    expect(useAuthStore().token).toBeNull();
  });

  it('never exposes a refresh token to the store on a successful exchange', async () => {
    server.use(
      http.post('/api/auth/callback', () =>
        HttpResponse.json({ accessToken: buildKeycloakToken(), expiresIn: 300, returnUrl: '/' }),
      ),
    );

    routeQuery.value = { code: 'the-code', state: 'the-state' };
    mountCallback();
    await flushPromises();

    // The renewal timer handle is a Node Timeout with circular refs, and holds no token.
    const { renewalTimer: _renewalTimer, ...serializableState } = useAuthStore().$state;
    expect(JSON.stringify(serializableState)).not.toContain('refresh');
  });
});
