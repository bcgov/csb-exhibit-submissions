import { createPinia, setActivePinia } from 'pinia';
import { useAuthStore } from '@/stores/authStore';
import router from '@/router';

function makeJwt(payload: object): string {
  const encode = (obj: unknown) => Buffer.from(JSON.stringify(obj)).toString('base64url');
  return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode(payload)}.test-sig`;
}

function tokenFor(role: string): string {
  return makeJwt({
    sub: `${role.toLowerCase()}@gov.bc.ca`,
    email: `${role.toLowerCase()}@gov.bc.ca`,
    role,
    exp: Math.floor(Date.now() / 1000) + 7200,
  });
}

beforeEach(async () => {
  localStorage.clear();
  setActivePinia(createPinia());
  // Reset to a known, unauthenticated starting point before each test.
  await router.push('/');
});

describe('router role guard', () => {
  it('redirects an unauthenticated visitor to Login', async () => {
    await router.push('/admin/list');
    expect(router.currentRoute.value.name).toBe('Login');
  });

  it('forbids a User (officer) from Submission Listing', async () => {
    useAuthStore().setToken(tokenFor('User'));
    await router.push('/admin/list');
    expect(router.currentRoute.value.name).toBe('Forbidden');
  });

  it('allows Clerk into Submission Listing', async () => {
    useAuthStore().setToken(tokenFor('Clerk'));
    await router.push('/admin/list');
    expect(router.currentRoute.value.name).toBe('AdminSubmissionList');
  });

  it('allows Admin into Submission Listing (shared access)', async () => {
    useAuthStore().setToken(tokenFor('Admin'));
    await router.push('/admin/list');
    expect(router.currentRoute.value.name).toBe('AdminSubmissionList');
  });

  it('allows Clerk into Submission Review', async () => {
    useAuthStore().setToken(tokenFor('Clerk'));
    await router.push('/admin/view/1');
    expect(router.currentRoute.value.name).toBe('AdminViewSubmission');
  });

  it('forbids Clerk from Exhibit Search (Admin/JJ-exclusive)', async () => {
    useAuthStore().setToken(tokenFor('Clerk'));
    await router.push('/admin/exhibit-search');
    expect(router.currentRoute.value.name).toBe('Forbidden');
  });

  it('allows Admin into Exhibit Search', async () => {
    useAuthStore().setToken(tokenFor('Admin'));
    await router.push('/admin/exhibit-search');
    expect(router.currentRoute.value.name).toBe('AdminExhibitSearch');
  });
});
