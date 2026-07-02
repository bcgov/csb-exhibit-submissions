import { mount, flushPromises } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import SubmissionForm from '@/components/officer/SubmissionForm.vue';
import { useCourtFileSelectionStore } from '@/stores/useCourtFileSelectionStore';
import type { CourtFileList } from '@/models/CourtFileList';

const mockPush = vi.hoisted(() => vi.fn());
vi.mock('vue-router', () => ({
  useRouter: () => ({ push: mockPush }),
}));

const mockSubmitExhibits = vi.hoisted(() => vi.fn());
const mockGetSubmissionsByFileNumber = vi.hoisted(() => vi.fn());
vi.mock('@/services/SubmissionService', () => ({
  default: () => ({
    submitExhibits: mockSubmitExhibits,
    getSubmissionsByFileNumber: mockGetSubmissionsByFileNumber,
  }),
}));

const mockTicket: CourtFileList = {
  appearanceId: 'APP001',
  appearanceDateTime: '2026-01-01T09:00:00',
  appearanceSequenceNumber: '001',
  appearanceReasonCode: 'ADP',
  courtListType: 'Criminal',
  fileNumberText: 'FILE001',
  locationId: 'LOC001',
  locationNameText: 'Test Court',
  roomCode: 'ROOM1',
  roomText: 'Courtroom 1',
  accusedName: 'Smith, John',
  accusedDOB: '1980-01-01',
  appearanceDetails: [
    { countPrintSequenceNumber: '001', statuteDescription: 'Test', appearanceReasonCode: 'ADP' },
  ],
};

function mountWithTickets() {
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useCourtFileSelectionStore();
  store.setSelectedFiles([mockTicket]);
  mockGetSubmissionsByFileNumber.mockResolvedValue([]);
  return mount(SubmissionForm, { global: { plugins: [pinia] } });
}

beforeEach(() => {
  localStorage.clear();
  setActivePinia(createPinia());
  mockPush.mockClear();
  mockSubmitExhibits.mockReset();
  mockGetSubmissionsByFileNumber.mockReset();
});

describe('SubmissionForm', () => {
  it('redirects to OfficerCourtList on mount when no tickets are selected', async () => {
    mockGetSubmissionsByFileNumber.mockResolvedValue([]);
    const pinia = createPinia();
    setActivePinia(pinia);
    mount(SubmissionForm, { global: { plugins: [pinia] } });
    await flushPromises();
    expect(mockPush).toHaveBeenCalledWith({ name: 'OfficerCourtList' });
  });

  it('stays on screen and shows success message after successful upload', async () => {
    mockSubmitExhibits.mockResolvedValue(true);
    const wrapper = mountWithTickets();
    await flushPromises();

    mockPush.mockClear();

    await wrapper.find('form').trigger('submit');
    await flushPromises();

    expect(mockPush).not.toHaveBeenCalled();
    expect(wrapper.find('.success-text').exists()).toBe(true);
    expect(wrapper.find('.success-text').text()).toContain('Exhibit uploaded successfully');
  });

  it('refreshes prior exhibits after successful upload', async () => {
    mockSubmitExhibits.mockResolvedValue(true);
    const wrapper = mountWithTickets();
    await flushPromises();

    const callsBefore = mockGetSubmissionsByFileNumber.mock.calls.length;

    await wrapper.find('form').trigger('submit');
    await flushPromises();

    expect(mockGetSubmissionsByFileNumber.mock.calls.length).toBeGreaterThan(callsBefore);
  });

  it('clears the progress bar after successful upload', async () => {
    mockSubmitExhibits.mockResolvedValue(true);
    const wrapper = mountWithTickets();
    await flushPromises();

    await wrapper.find('form').trigger('submit');
    await flushPromises();

    const bar = wrapper.find('.progress-bar');
    expect(bar.attributes('style')).toContain('width: 0%');
  });

  it('shows error message and stays on screen when upload fails', async () => {
    mockSubmitExhibits.mockResolvedValue(false);
    const wrapper = mountWithTickets();
    await flushPromises();

    mockPush.mockClear();

    await wrapper.find('form').trigger('submit');
    await flushPromises();

    expect(mockPush).not.toHaveBeenCalled();
    expect(wrapper.find('.error-text').exists()).toBe(true);
  });

  it('shows error message and stays on screen when upload throws', async () => {
    mockSubmitExhibits.mockRejectedValue(new Error('Network error'));
    const wrapper = mountWithTickets();
    await flushPromises();

    mockPush.mockClear();

    await wrapper.find('form').trigger('submit');
    await flushPromises();

    expect(mockPush).not.toHaveBeenCalled();
    expect(wrapper.find('.error-text').exists()).toBe(true);
    expect(wrapper.find('.error-text').text()).toContain('Failed to upload');
  });
});
