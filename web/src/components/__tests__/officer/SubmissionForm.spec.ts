import { mount, flushPromises } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import SubmissionForm from '@/components/officer/SubmissionForm.vue';
import { useAuthStore } from '@/stores/authStore';
import { useCourtFileSelectionStore } from '@/stores/useCourtFileSelectionStore';
import type { CourtFileList } from '@/models/CourtFileList';

vi.mock('@/services/UserService', () => ({
  default: () => ({
    getProfile: vi.fn(),
    saveOfficerNumber: vi.fn(),
  }),
}));

const mockPush = vi.hoisted(() => vi.fn());
vi.mock('vue-router', () => ({
  useRouter: () => ({ push: mockPush }),
}));

const mockSubmitExhibits = vi.hoisted(() => vi.fn());
const mockGetSubmissionsByFileNumber = vi.hoisted(() => vi.fn());
const mockGetFileHistory = vi.hoisted(() => vi.fn());
const mockGetExhibitNotes = vi.hoisted(() => vi.fn());
vi.mock('@/services/SubmissionService', () => ({
  default: () => ({
    submitExhibits: mockSubmitExhibits,
    getSubmissionsByFileNumber: mockGetSubmissionsByFileNumber,
    getFileHistory: mockGetFileHistory,
    getExhibitNotes: mockGetExhibitNotes,
    addExhibitDescription: vi.fn(),
    markExhibit: vi.fn(),
    enterExhibit: vi.fn(),
    updateEvidenceSource: vi.fn(),
  }),
}));

const priorSubmission = {
  submissionId: 5,
  submissionDate: '2026-07-07T09:00:00Z',
  appearanceDateTime: '2026-07-07T09:00:00',
  location: 'Test Court',
  room: 'Courtroom 1',
  files: [
    {
      id: 'file-1',
      originalFileName: 'exhibit.mp4',
      storedFileName: 'stored.mp4',
      viewUrl: '',
      downloadUrl: '',
      contentType: 'video/mp4',
      fileSize: 1024,
      storageProvider: 'Local',
      status: 'Unclassified',
      descriptions: [],
    },
  ],
};

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

const secondTicket: CourtFileList = {
  ...mockTicket,
  appearanceId: 'APP002',
  fileNumberText: 'FILE002',
};

/**
 * @param officerNumber The number stored on the officer's profile. Null models an officer
 *   who has not answered the Court Search prompt yet, which blocks upload (CES-27).
 */
function mountWithTickets(
  tickets: CourtFileList[] = [mockTicket],
  officerNumber: string | null = 'PC-1234',
) {
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useCourtFileSelectionStore();
  store.setSelectedFiles(tickets);
  useAuthStore().setOfficerNumber(officerNumber);
  mockGetSubmissionsByFileNumber.mockResolvedValue([]);
  return mount(SubmissionForm, { global: { plugins: [pinia] } });
}

beforeEach(() => {
  localStorage.clear();
  setActivePinia(createPinia());
  mockPush.mockClear();
  mockSubmitExhibits.mockReset();
  mockGetSubmissionsByFileNumber.mockReset();
  mockGetFileHistory.mockReset().mockResolvedValue([]);
  mockGetExhibitNotes.mockReset().mockResolvedValue([]);
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
    mockSubmitExhibits.mockResolvedValue(7);
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
    mockSubmitExhibits.mockResolvedValue(7);
    const wrapper = mountWithTickets();
    await flushPromises();

    const callsBefore = mockGetSubmissionsByFileNumber.mock.calls.length;

    await wrapper.find('form').trigger('submit');
    await flushPromises();

    expect(mockGetSubmissionsByFileNumber.mock.calls.length).toBeGreaterThan(callsBefore);
  });

  it('clears the progress bar after successful upload', async () => {
    mockSubmitExhibits.mockResolvedValue(7);
    const wrapper = mountWithTickets();
    await flushPromises();

    await wrapper.find('form').trigger('submit');
    await flushPromises();

    const bar = wrapper.find('.progress-bar');
    expect(bar.attributes('style')).toContain('width: 0%');
  });

  it('shows error message and stays on screen when upload fails', async () => {
    mockSubmitExhibits.mockResolvedValue(null);
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

  it('passes the active submission id to submitExhibits on subsequent uploads', async () => {
    mockSubmitExhibits.mockResolvedValue(7);
    const wrapper = mountWithTickets();
    await flushPromises();

    // First upload: no active submission yet.
    await wrapper.find('form').trigger('submit');
    await flushPromises();
    expect(mockSubmitExhibits.mock.calls[0][3]).toBeNull();

    // Second upload: attaches to the submission returned by the first.
    await wrapper.find('form').trigger('submit');
    await flushPromises();
    expect(mockSubmitExhibits.mock.calls[1][3]).toBe(7);
  });

  it('hides the remove-ticket buttons once a submission is active', async () => {
    mockSubmitExhibits.mockResolvedValue(7);
    const wrapper = mountWithTickets([mockTicket, secondTicket]);
    await flushPromises();

    // Two tickets → remove buttons are shown before the first upload.
    expect(wrapper.findAll('.remove-btn').length).toBe(2);

    await wrapper.find('form').trigger('submit');
    await flushPromises();

    // After the first upload the ticket set is locked.
    expect(wrapper.findAll('.remove-btn').length).toBe(0);
  });

  // CES-42: officers get the exhibit detail modal, minus the registry-only Notes section.
  it('opens the exhibit detail modal from the filename, without the Notes section', async () => {
    const pinia = createPinia();
    setActivePinia(pinia);
    useCourtFileSelectionStore().setSelectedFiles([mockTicket]);
    mockGetSubmissionsByFileNumber.mockResolvedValue([priorSubmission]);
    const wrapper = mount(SubmissionForm, { global: { plugins: [pinia] } });
    await flushPromises();

    expect(wrapper.find('.exhibit-detail-dialog').exists()).toBe(false);

    await wrapper.find('.prior-file-name-link').trigger('click');
    await flushPromises();

    expect(wrapper.find('.exhibit-detail-dialog').exists()).toBe(true);
    expect(wrapper.find('.descriptions-section').exists()).toBe(true);
    expect(wrapper.find('.notes-section').exists()).toBe(false);
    expect(mockGetExhibitNotes).not.toHaveBeenCalled();
  });

  // CES-27: the officer number comes from the profile, not from this form.
  describe('officer number', () => {
    it('prefills the field from the store and leaves it disabled', async () => {
      const wrapper = mountWithTickets();
      await flushPromises();

      const field = wrapper.find('#officerNumberField');
      expect((field.element as HTMLInputElement).value).toBe('PC-1234');
      expect(field.attributes('disabled')).toBeDefined();
    });

    it('sends the stored number with the submission', async () => {
      mockSubmitExhibits.mockResolvedValue(7);
      const wrapper = mountWithTickets();
      await flushPromises();

      await wrapper.find('form').trigger('submit');
      await flushPromises();

      expect(mockSubmitExhibits.mock.calls[0][0].officerNumber).toBe('PC-1234');
    });

    it('disables upload and warns when no number is stored', async () => {
      const wrapper = mountWithTickets([mockTicket], null);
      await flushPromises();

      expect(wrapper.find('button[type="submit"]').attributes('disabled')).toBeDefined();
      expect(wrapper.find('.officer-field-missing').exists()).toBe(true);
    });

    it('does not submit without a stored number', async () => {
      const wrapper = mountWithTickets([mockTicket], null);
      await flushPromises();

      await wrapper.find('form').trigger('submit');
      await flushPromises();

      expect(mockSubmitExhibits).not.toHaveBeenCalled();
      // Prompted instead of failed — the modal is the only way to supply one.
      expect(wrapper.find('.officer-number-dialog').exists()).toBe(true);
    });

    it('opens the modal from the Edit button', async () => {
      const wrapper = mountWithTickets();
      await flushPromises();

      expect(wrapper.find('.officer-number-dialog').exists()).toBe(false);

      await wrapper.find('.officer-number-edit').trigger('click');

      expect(wrapper.find('.officer-number-dialog').exists()).toBe(true);
    });

    it('reflects a number saved through the modal without a remount', async () => {
      const wrapper = mountWithTickets([mockTicket], null);
      await flushPromises();

      useAuthStore().setOfficerNumber('PC-9999');
      await flushPromises();

      expect((wrapper.find('#officerNumberField').element as HTMLInputElement).value).toBe(
        'PC-9999',
      );
      expect(wrapper.find('button[type="submit"]').attributes('disabled')).toBeUndefined();
    });
  });
});
