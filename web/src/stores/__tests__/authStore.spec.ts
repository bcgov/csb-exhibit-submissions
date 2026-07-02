import { createPinia, setActivePinia } from 'pinia';
import { useAuthStore } from '@/stores/authStore';

function makeJwt(payload: object): string {
  const encode = (obj: unknown) => Buffer.from(JSON.stringify(obj)).toString('base64url');
  return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode(payload)}.test-sig`;
}

const validJwt = makeJwt({
  sub: 'admin@gov.bc.ca',
  email: 'admin@gov.bc.ca',
  role: 'Admin',
  exp: Math.floor(Date.now() / 1000) + 7200,
});

const expiredJwt = makeJwt({
  sub: 'user@gov.bc.ca',
  email: 'user@gov.bc.ca',
  role: 'User',
  exp: Math.floor(Date.now() / 1000) - 3600,
});

beforeEach(() => {
  localStorage.clear();
  setActivePinia(createPinia());
});

describe('authStore', () => {
  it('isAuthenticated returns false when no token', () => {
    const store = useAuthStore();
    expect(store.isAuthenticated).toBe(false);
  });

  it('setToken stores token and decodes user', () => {
    const store = useAuthStore();
    store.setToken(validJwt);
    expect(store.token).toBe(validJwt);
    expect(store.user).not.toBeNull();
    expect(store.user?.id).toBe('admin@gov.bc.ca');
  });

  it('isAuthenticated returns true with valid token', () => {
    const store = useAuthStore();
    store.setToken(validJwt);
    expect(store.isAuthenticated).toBe(true);
  });

  it('isAuthenticated returns false with expired token', () => {
    const store = useAuthStore();
    store.setToken(expiredJwt);
    expect(store.isAuthenticated).toBe(false);
  });

  it('clearAuth resets state', () => {
    const store = useAuthStore();
    store.setToken(validJwt);
    store.clearAuth();
    expect(store.token).toBeNull();
    expect(store.user).toBeNull();
    expect(localStorage.getItem('jwt_token')).toBeNull();
  });

  it('hasRole returns true for matching role', () => {
    const store = useAuthStore();
    store.setToken(validJwt);
    expect(store.hasRole('Admin')).toBe(true);
  });
});
