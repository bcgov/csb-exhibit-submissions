import CourtListing from '@/components/officer/CourtListing.vue';
import { ROLE_ADMIN, ROLE_USER } from '@/constants/roles';
import { useAuthStore } from '@/stores/authStore';
import { flushPromises, mount } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';

vi.mock('vue-router', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/services/LocationService', () => ({
  default: () => ({ getLocations: vi.fn().mockResolvedValue([]) }),
}));

vi.mock('@/services/CourtFileService', () => ({
  default: () => ({ getCourtList: vi.fn().mockResolvedValue([]) }),
}));

const mockGetProfile = vi.hoisted(() => vi.fn());
vi.mock('@/services/UserService', () => ({
  default: () => ({
    getProfile: mockGetProfile,
    saveOfficerNumber: vi.fn(),
  }),
}));

function makeJwt(role: string): string {
  const encode = (obj: unknown) => Buffer.from(JSON.stringify(obj)).toString('base64url');
  const payload = {
    sub: 'officer@gov.bc.ca',
    email: 'officer@gov.bc.ca',
    role,
    exp: Math.floor(Date.now() / 1000) + 7200,
  };
  return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode(payload)}.test-sig`;
}

/**
 * Mounts Court Search for a signed-in user whose profile has already loaded.
 *
 * @param officerNumber Null models an officer who has never supplied one.
 */
async function mountFor(role: string, officerNumber: string | null) {
  const pinia = createPinia();
  setActivePinia(pinia);
  const authStore = useAuthStore();
  authStore.setToken(makeJwt(role));
  mockGetProfile.mockResolvedValue({
    id: 1,
    firstName: 'Dev',
    lastName: 'Officer',
    email: 'officer@gov.bc.ca',
    officerNumber,
  });
  await authStore.loadProfile();

  const wrapper = mount(CourtListing, { global: { plugins: [pinia] } });
  await flushPromises();
  return wrapper;
}

beforeEach(() => {
  localStorage.clear();
  setActivePinia(createPinia());
  mockGetProfile.mockReset();
});

describe('CourtListing officer number prompt', () => {
  it('prompts an officer who has no stored number', async () => {
    const wrapper = await mountFor(ROLE_USER, null);

    expect(wrapper.find('.officer-number-dialog').exists()).toBe(true);
  });

  it('does not prompt an officer who already has one', async () => {
    const wrapper = await mountFor(ROLE_USER, 'PC-1234');

    expect(wrapper.find('.officer-number-dialog').exists()).toBe(false);
  });

  it('does not prompt an admin', async () => {
    // Admin/Clerk have no officer number and are never asked for one.
    const wrapper = await mountFor(ROLE_ADMIN, null);

    expect(wrapper.find('.officer-number-dialog').exists()).toBe(false);
  });

  it('does not prompt before the profile has loaded', async () => {
    // The fetch is async; prompting on an unanswered profile would flash the modal at an
    // officer who does have a number stored.
    const pinia = createPinia();
    setActivePinia(pinia);
    useAuthStore().setToken(makeJwt(ROLE_USER));

    const wrapper = mount(CourtListing, { global: { plugins: [pinia] } });
    await flushPromises();

    expect(wrapper.find('.officer-number-dialog').exists()).toBe(false);
  });

  it('prompts as soon as the profile load resolves after mount', async () => {
    const pinia = createPinia();
    setActivePinia(pinia);
    const authStore = useAuthStore();
    authStore.setToken(makeJwt(ROLE_USER));
    mockGetProfile.mockResolvedValue({
      id: 1,
      firstName: 'Dev',
      lastName: 'Officer',
      email: 'officer@gov.bc.ca',
      officerNumber: null,
    });

    const wrapper = mount(CourtListing, { global: { plugins: [pinia] } });
    await flushPromises();
    expect(wrapper.find('.officer-number-dialog').exists()).toBe(false);

    await authStore.loadProfile();
    await flushPromises();

    expect(wrapper.find('.officer-number-dialog').exists()).toBe(true);
  });

  it('stays dismissed for the rest of the visit once closed', async () => {
    const wrapper = await mountFor(ROLE_USER, null);

    await wrapper.findComponent({ name: 'OfficerNumberModal' }).vm.$emit('close');
    await flushPromises();

    expect(wrapper.find('.officer-number-dialog').exists()).toBe(false);
  });

  it('closes once a number is saved to the store', async () => {
    const wrapper = await mountFor(ROLE_USER, null);

    useAuthStore().setOfficerNumber('PC-1234');
    await flushPromises();

    expect(wrapper.find('.officer-number-dialog').exists()).toBe(false);
  });
});
