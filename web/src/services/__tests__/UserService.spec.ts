import { http, HttpResponse } from 'msw';
import { createPinia, setActivePinia } from 'pinia';
import useUserService from '@/services/UserService';
import { server } from '@/test/setup';

vi.mock('@/router', () => ({
  default: {
    push: vi.fn(),
    currentRoute: { value: { path: '/' } },
  },
}));

const profile = {
  id: 1,
  firstName: 'Dev',
  lastName: 'Officer',
  email: 'officer@gov.bc.ca',
  officerNumber: 'PC-1234',
};

beforeEach(() => {
  localStorage.clear();
  setActivePinia(createPinia());
});

describe('UserService', () => {
  it('getProfile returns the profile from /api/users/me', async () => {
    server.use(http.get('/api/users/me', () => HttpResponse.json(profile)));

    expect(await useUserService().getProfile()).toEqual(profile);
  });

  it('getProfile resolves to null on 404 rather than throwing', async () => {
    // A signed-in user with no local row is an ordinary state, not a failure.
    server.use(http.get('/api/users/me', () => new HttpResponse(null, { status: 404 })));

    expect(await useUserService().getProfile()).toBeNull();
  });

  it('getProfile rethrows other failures', async () => {
    server.use(http.get('/api/users/me', () => new HttpResponse(null, { status: 500 })));

    await expect(useUserService().getProfile()).rejects.toThrow();
  });

  it('saveOfficerNumber PUTs the value and returns the updated profile', async () => {
    let body: unknown;
    server.use(
      http.put('/api/users/me/officer-number', async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(profile);
      }),
    );

    const result = await useUserService().saveOfficerNumber('PC-1234');

    expect(body).toEqual({ officerNumber: 'PC-1234' });
    expect(result.officerNumber).toBe('PC-1234');
  });

  it('saveOfficerNumber rejects when the API refuses the value', async () => {
    server.use(
      http.put('/api/users/me/officer-number', () =>
        HttpResponse.json({ message: 'An officer number is required.' }, { status: 400 }),
      ),
    );

    await expect(useUserService().saveOfficerNumber('')).rejects.toThrow();
  });
});
