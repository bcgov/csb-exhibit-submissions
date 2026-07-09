import { mount, flushPromises } from '@vue/test-utils';
import ExhibitDetailModal from '@/components/admin/ExhibitDetailModal.vue';
import type { ExhibitSearchResultModel } from '@/models/ExhibitSearchResultModel';
import type { SubmissionFile } from '@/models/SubmissionReviewModel';

const mockGetFileHistory = vi.hoisted(() => vi.fn());
const mockGetExhibitNotes = vi.hoisted(() => vi.fn());
const mockAddExhibitNote = vi.hoisted(() => vi.fn());

vi.mock('@/services/SubmissionService', () => ({
  default: () => ({
    getFileHistory: mockGetFileHistory,
    getExhibitNotes: mockGetExhibitNotes,
    addExhibitNote: mockAddExhibitNote,
  }),
}));

const makeFile = (overrides: Partial<SubmissionFile> = {}): SubmissionFile => ({
  id: 'file-1',
  originalFileName: 'exhibit-a.pdf',
  storedFileName: 'stored.pdf',
  viewUrl: '',
  downloadUrl: '',
  contentType: 'application/pdf',
  fileSize: 2048,
  storageProvider: 'Local',
  status: 'Marked',
  markedValue: 'D',
  ...overrides,
});

const makeResult = (overrides: Partial<ExhibitSearchResultModel> = {}): ExhibitSearchResultModel => ({
  file: makeFile(),
  submissionId: 7,
  submissionDate: '2026-07-07T09:00:00Z',
  appearanceDateTime: '2026-07-07T09:00:00',
  location: 'Test Court',
  room: 'ROOM1',
  fileNumbers: ['FILE001', 'FILE002'],
  accusedName: 'Smith, John',
  ...overrides,
});

function mountModal(result = makeResult()) {
  return mount(ExhibitDetailModal, { props: { result } });
}

beforeEach(() => {
  mockGetFileHistory.mockReset().mockResolvedValue([]);
  mockGetExhibitNotes.mockReset().mockResolvedValue([]);
  mockAddExhibitNote.mockReset();
});

describe('ExhibitDetailModal', () => {
  it('loads history and notes on mount and renders them', async () => {
    mockGetFileHistory.mockResolvedValue([
      {
        fieldName: 'MarkedValue',
        oldValue: null,
        newValue: 'D',
        changedBy: 'admin@gov.bc.ca',
        changedAtUTC: '2026-07-07T09:00:00Z',
      },
    ]);
    mockGetExhibitNotes.mockResolvedValue([
      { id: 1, noteText: 'existing note', createdBy: 'admin', createdAtUTC: '2026-07-07T09:30:00Z' },
    ]);

    const wrapper = mountModal();
    await flushPromises();

    expect(mockGetFileHistory).toHaveBeenCalledWith('file-1');
    expect(mockGetExhibitNotes).toHaveBeenCalledWith('file-1');
    expect(wrapper.find('.detail-history-table').exists()).toBe(true);
    expect(wrapper.text()).toContain('existing note');
  });

  it('renders the "Registry use only" badge and file details', async () => {
    const wrapper = mountModal();
    await flushPromises();

    expect(wrapper.find('.registry-badge').text()).toBe('Registry use only');
    expect(wrapper.text()).toContain('FILE001, FILE002');
    expect(wrapper.text()).toContain('Smith, John');
  });

  it('disables Save when the note is empty and enables it once text is entered', async () => {
    const wrapper = mountModal();
    await flushPromises();

    const saveBtn = wrapper.find('.btn-save-note');
    expect(saveBtn.attributes('disabled')).toBeDefined();

    await wrapper.find('#new-note').setValue('a new note');
    expect(saveBtn.attributes('disabled')).toBeUndefined();
  });

  it('appends a saved note and clears the input', async () => {
    mockAddExhibitNote.mockResolvedValue({
      id: 9,
      noteText: 'a new note',
      createdBy: 'admin',
      createdAtUTC: '2026-07-07T11:00:00Z',
    });
    const wrapper = mountModal();
    await flushPromises();

    await wrapper.find('#new-note').setValue('a new note');
    await wrapper.find('.btn-save-note').trigger('click');
    await flushPromises();

    expect(mockAddExhibitNote).toHaveBeenCalledWith('file-1', 'a new note');
    expect(wrapper.text()).toContain('a new note');
    expect((wrapper.find('#new-note').element as HTMLTextAreaElement).value).toBe('');
  });

  it('emits close when the close button is clicked', async () => {
    const wrapper = mountModal();
    await flushPromises();

    await wrapper.find('.close').trigger('click');

    expect(wrapper.emitted('close')).toHaveLength(1);
  });
});
