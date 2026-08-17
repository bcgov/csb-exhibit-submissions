import OfficerNumberModal from '@/components/officer/OfficerNumberModal.vue';
import { useAuthStore } from '@/stores/authStore';
import { flushPromises, mount } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';

const mockSaveOfficerNumber = vi.hoisted(() => vi.fn());
vi.mock('@/services/UserService', () => ({
  default: () => ({
    saveOfficerNumber: mockSaveOfficerNumber,
    getProfile: vi.fn(),
  }),
}));

function mountModal(initialValue?: string | null) {
  const pinia = createPinia();
  setActivePinia(pinia);
  return mount(OfficerNumberModal, {
    props: { initialValue },
    global: { plugins: [pinia] },
  });
}

const savedProfile = {
  id: 1,
  firstName: 'Dev',
  lastName: 'Officer',
  email: 'officer@gov.bc.ca',
  officerNumber: 'PC-1234',
};

beforeEach(() => {
  localStorage.clear();
  setActivePinia(createPinia());
  mockSaveOfficerNumber.mockReset();
});

describe('OfficerNumberModal', () => {
  it('disables Save until something is entered', async () => {
    const wrapper = mountModal();

    const save = wrapper.find('button[type="submit"]');
    expect(save.attributes('disabled')).toBeDefined();

    await wrapper.find('input').setValue('PC1234');

    expect(wrapper.find('button[type="submit"]').attributes('disabled')).toBeUndefined();
  });

  it('prefills the input when editing an existing number', () => {
    const wrapper = mountModal('PC-1234');

    expect((wrapper.find('input').element as HTMLInputElement).value).toBe('PC-1234');
  });

  it('strips disallowed characters as they are typed', async () => {
    const wrapper = mountModal();

    await wrapper.find('input').setValue('AB 12/34');

    expect((wrapper.find('input').element as HTMLInputElement).value).toBe('AB1234');
  });

  it('clamps typed input to the maximum length', async () => {
    const wrapper = mountModal();

    await wrapper.find('input').setValue('A'.repeat(40));

    expect((wrapper.find('input').element as HTMLInputElement).value).toHaveLength(30);
  });

  it('saves, updates the store, and emits saved then close', async () => {
    mockSaveOfficerNumber.mockResolvedValue(savedProfile);
    const wrapper = mountModal();
    await wrapper.find('input').setValue('PC-1234');

    await wrapper.find('form').trigger('submit');
    await flushPromises();

    expect(mockSaveOfficerNumber).toHaveBeenCalledWith('PC-1234');
    expect(useAuthStore().officerNumber).toBe('PC-1234');
    expect(wrapper.emitted('saved')?.[0]).toEqual(['PC-1234']);
    expect(wrapper.emitted('close')).toBeTruthy();
  });

  it('stays open and shows the API message when the save is rejected', async () => {
    mockSaveOfficerNumber.mockRejectedValue({
      isAxiosError: true,
      response: { status: 400, data: { message: 'An officer number is required.' } },
    });
    const wrapper = mountModal();
    await wrapper.find('input').setValue('PC1234');

    await wrapper.find('form').trigger('submit');
    await flushPromises();

    expect(wrapper.emitted('close')).toBeFalsy();
    expect(wrapper.find('.officer-number-error').text()).toBe('An officer number is required.');
    expect(useAuthStore().officerNumber).toBeNull();
  });

  it('falls back to a generic message when the failure is not an API rejection', async () => {
    mockSaveOfficerNumber.mockRejectedValue(new Error('offline'));
    const wrapper = mountModal();
    await wrapper.find('input').setValue('PC1234');

    await wrapper.find('form').trigger('submit');
    await flushPromises();

    expect(wrapper.find('.officer-number-error').text()).toContain('Could not save');
  });

  it('emits close from Cancel without calling the API', async () => {
    const wrapper = mountModal();

    await wrapper.find('button[type="button"]').trigger('click');

    expect(mockSaveOfficerNumber).not.toHaveBeenCalled();
    expect(wrapper.emitted('close')).toBeTruthy();
  });

  it('emits close on Escape', async () => {
    const wrapper = mountModal();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    await flushPromises();

    expect(wrapper.emitted('close')).toBeTruthy();
  });

  it('emits close on a backdrop click', async () => {
    const wrapper = mountModal();

    await wrapper.find('.officer-number-overlay').trigger('click');

    expect(wrapper.emitted('close')).toBeTruthy();
  });

  it('stops listening for Escape once unmounted', async () => {
    const wrapper = mountModal();
    wrapper.unmount();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    await flushPromises();

    expect(wrapper.emitted('close')).toBeFalsy();
  });
});
