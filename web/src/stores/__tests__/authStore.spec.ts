import { createPinia, setActivePinia } from 'pinia';
import { useAuthStore } from '@/stores/authStore';

const mockGetProfile = vi.hoisted(() => vi.fn());
vi.mock('@/services/UserService', () => ({
  default: () => ({
    getProfile: mockGetProfile,
    saveOfficerNumber: vi.fn(),
  }),
}));

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

// A second, longer-lived Admin token — what a renewal hands back mid-session.
const renewedJwt = makeJwt({
  sub: 'admin@gov.bc.ca',
  email: 'admin@gov.bc.ca',
  role: 'Admin',
  exp: Math.floor(Date.now() / 1000) + 14400,
});

const clerkJwt = makeJwt({
  sub: 'clerk@gov.bc.ca',
  email: 'clerk@gov.bc.ca',
  role: 'Clerk',
  exp: Math.floor(Date.now() / 1000) + 7200,
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

  it('hasRole returns true for a Clerk token', () => {
    const store = useAuthStore();
    store.setToken(clerkJwt);
    expect(store.hasRole('Clerk')).toBe(true);
    expect(store.hasRole('Admin')).toBe(false);
  });

  it('user.roles matches the roles ref used by hasRole', () => {
    const store = useAuthStore();
    store.setToken(clerkJwt);
    expect(store.user?.roles).toEqual(store.roles);
    expect(store.user?.roles).toEqual(['Clerk']);
  });
});

// The officer number is not a token claim (CES-27) — it is fetched from the API and has to
// outlive the token renewals that rebuild `user` from scratch.
describe('authStore officer number', () => {
  beforeEach(() => {
    mockGetProfile.mockReset().mockResolvedValue({
      id: 1,
      firstName: 'Dev',
      lastName: 'Officer',
      email: 'officer@gov.bc.ca',
      officerNumber: 'PC-1234',
    });
  });

  it('is null and hasOfficerNumber false before the profile loads', () => {
    const store = useAuthStore();
    expect(store.officerNumber).toBeNull();
    expect(store.hasOfficerNumber).toBe(false);
    expect(store.profileLoaded).toBe(false);
  });

  it('loadProfile populates the officer number and marks the profile loaded', async () => {
    const store = useAuthStore();

    await store.loadProfile();

    expect(store.officerNumber).toBe('PC-1234');
    expect(store.hasOfficerNumber).toBe(true);
    expect(store.profileLoaded).toBe(true);
  });

  it('loadProfile is single-flight across concurrent callers', async () => {
    const store = useAuthStore();

    await Promise.all([store.loadProfile(), store.loadProfile(), store.loadProfile()]);

    expect(mockGetProfile).toHaveBeenCalledTimes(1);
  });

  it('loadProfile is a no-op once loaded, so renewals do not refetch', async () => {
    const store = useAuthStore();

    await store.loadProfile();
    await store.loadProfile();

    expect(mockGetProfile).toHaveBeenCalledTimes(1);
  });

  it('loadProfile swallows a failed fetch and stays unloaded so it can retry', async () => {
    mockGetProfile.mockRejectedValue(new Error('offline'));
    const store = useAuthStore();

    await store.loadProfile();

    expect(store.officerNumber).toBeNull();
    expect(store.profileLoaded).toBe(false);
  });

  it('marks the profile loaded even when the user has no officer number yet', async () => {
    mockGetProfile.mockResolvedValue({
      id: 1,
      firstName: 'Dev',
      lastName: 'Officer',
      email: 'officer@gov.bc.ca',
      officerNumber: null,
    });
    const store = useAuthStore();

    await store.loadProfile();

    // profileLoaded is what tells the Court Search prompt the answer is in.
    expect(store.profileLoaded).toBe(true);
    expect(store.hasOfficerNumber).toBe(false);
  });

  it('setOfficerNumber projects the value onto user', () => {
    const store = useAuthStore();
    store.setToken(validJwt);

    store.setOfficerNumber('PC-9999');

    expect(store.officerNumber).toBe('PC-9999');
    expect(store.user?.officerNumber).toBe('PC-9999');
  });

  it('survives a token renewal', async () => {
    const store = useAuthStore();
    store.setToken(validJwt);
    await store.loadProfile();

    // A renewal re-decodes a fresh token and rebuilds `user` — the number must not be lost.
    store.setToken(renewedJwt);

    expect(store.officerNumber).toBe('PC-1234');
    expect(store.user?.officerNumber).toBe('PC-1234');
  });

  it('clearAuth resets the officer number and allows a refetch on the next session', async () => {
    const store = useAuthStore();
    await store.loadProfile();

    store.clearAuth();

    expect(store.officerNumber).toBeNull();
    expect(store.profileLoaded).toBe(false);

    await store.loadProfile();
    expect(mockGetProfile).toHaveBeenCalledTimes(2);
  });
});
