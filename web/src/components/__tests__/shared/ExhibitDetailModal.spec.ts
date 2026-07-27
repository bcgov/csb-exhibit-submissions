import { mount, flushPromises } from '@vue/test-utils';
import ExhibitDetailModal from '@/components/shared/ExhibitDetailModal.vue';
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
  descriptions: [],
  ...overrides,
});

const makeResult = (
  overrides: Partial<ExhibitSearchResultModel> = {},
): ExhibitSearchResultModel => ({
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

// Admin view: registry notes visible, appending always allowed.
function mountModal(result = makeResult(), props: Record<string, unknown> = {}) {
  return mount(ExhibitDetailModal, {
    props: { result, canViewNotes: true, alwaysEditable: true, ...props },
  });
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

// CES-42: officers open the same modal, minus the registry-only Notes section.
describe('ExhibitDetailModal — officer view', () => {
  it('renders no Notes section and never calls the admin-only notes endpoint', async () => {
    const wrapper = mount(ExhibitDetailModal, { props: { result: makeResult() } });
    await flushPromises();

    expect(wrapper.find('.notes-section').exists()).toBe(false);
    expect(wrapper.find('.registry-badge').exists()).toBe(false);
    expect(mockGetExhibitNotes).not.toHaveBeenCalled();
    // Descriptions are not registry-only — that section stays.
    expect(wrapper.find('.descriptions-section').exists()).toBe(true);
  });
});

describe('ExhibitDetailModal — description entries', () => {
  const withDescriptions = () =>
    makeResult({
      file: makeFile({
        descriptions: [
          {
            id: 1,
            descriptionText: 'first line\nsecond line',
            createdBy: 'officer@test.ca',
            createdAtUTC: '2026-07-07T09:00:00Z',
          },
          {
            id: 2,
            descriptionText: 'an addendum',
            createdBy: 'admin@test.ca',
            createdAtUTC: '2026-07-07T10:00:00Z',
          },
        ],
      }),
    });

  it('lists every entry oldest first, preserving line breaks', async () => {
    const wrapper = mountModal(withDescriptions());
    await flushPromises();

    const entries = wrapper.findAll('.descriptions-section .entry-text');
    expect(entries).toHaveLength(2);
    expect(entries[0].text()).toContain('first line\nsecond line');
    expect(entries[1].text()).toBe('an addendum');
  });

  it('appends an entry, clears the input, and emits fileUpdated', async () => {
    const addDescriptionFn = vi.fn().mockResolvedValue(
      makeFile({
        descriptions: [
          {
            id: 3,
            descriptionText: 'a later addendum',
            createdBy: 'admin@test.ca',
            createdAtUTC: '2026-07-07T12:00:00Z',
          },
        ],
      }),
    );
    const wrapper = mountModal(makeResult(), { addDescriptionFn });
    await flushPromises();

    await wrapper.find('#new-description').setValue('a later addendum');
    await wrapper.find('.btn-save-description').trigger('click');
    await flushPromises();

    expect(addDescriptionFn).toHaveBeenCalledWith('file-1', 'a later addendum');
    expect(wrapper.text()).toContain('a later addendum');
    expect((wrapper.find('#new-description').element as HTMLTextAreaElement).value).toBe('');
    expect(wrapper.emitted('fileUpdated')).toHaveLength(1);
  });

  it('renders the history read-only when no addDescriptionFn is supplied', async () => {
    const wrapper = mount(ExhibitDetailModal, { props: { result: withDescriptions() } });
    await flushPromises();

    expect(wrapper.findAll('.descriptions-section .entry-text')).toHaveLength(2);
    expect(wrapper.find('#new-description').exists()).toBe(false);
  });

  it('hides the input from an officer once the exhibit is Entered', async () => {
    const result = makeResult({ file: makeFile({ status: 'Entered', enteredValue: '3' }) });

    const officer = mount(ExhibitDetailModal, {
      props: { result, addDescriptionFn: vi.fn() },
    });
    await flushPromises();
    expect(officer.find('#new-description').exists()).toBe(false);

    // Admin (alwaysEditable) keeps it.
    const admin = mountModal(result, { addDescriptionFn: vi.fn() });
    await flushPromises();
    expect(admin.find('#new-description').exists()).toBe(true);
  });
});
