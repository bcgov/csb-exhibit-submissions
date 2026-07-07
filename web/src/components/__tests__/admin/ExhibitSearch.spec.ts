import { mount, flushPromises } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import ExhibitSearch from '@/components/admin/ExhibitSearch.vue';
import type { ExhibitSearchResultModel } from '@/models/ExhibitSearchResultModel';
import type { SubmissionFile } from '@/models/SubmissionReviewModel';

const mockSearchExhibits = vi.hoisted(() => vi.fn());
const mockMarkExhibit = vi.hoisted(() => vi.fn());
const mockEnterExhibit = vi.hoisted(() => vi.fn());
const mockUpdateExhibitDescription = vi.hoisted(() => vi.fn());

vi.mock('@/services/SubmissionService', () => ({
  default: () => ({
    searchExhibits: mockSearchExhibits,
    markExhibit: mockMarkExhibit,
    enterExhibit: mockEnterExhibit,
    updateExhibitDescription: mockUpdateExhibitDescription,
  }),
}));

const makeFile = (overrides: Partial<SubmissionFile> = {}): SubmissionFile => ({
  id: 'file-1',
  originalFileName: 'exhibit-a.pdf',
  storedFileName: 'stored.pdf',
  viewUrl: '',
  downloadUrl: '',
  contentType: 'application/pdf',
  fileSize: 1024,
  storageProvider: 'Local',
  status: 'Unclassified',
  ...overrides,
});

const makeResult = (overrides: Partial<ExhibitSearchResultModel> = {}): ExhibitSearchResultModel => ({
  file: makeFile(),
  submissionId: 1,
  submissionDate: '2026-07-07T09:00:00Z',
  appearanceDateTime: '2026-07-07T09:00:00',
  location: 'Test Court',
  room: 'ROOM1',
  fileNumbers: ['FILE001'],
  accusedName: 'Smith, John',
  ...overrides,
});

// Lightweight stub for the shared ExhibitList so we can inspect the props it receives
// and drive its events precisely (its own behaviour is covered by ExhibitList tests).
const ExhibitListStub = {
  props: [
    'entries',
    'alwaysEditable',
    'showRemoved',
    'canDownload',
    'canRemove',
    'linkableTitle',
    'markFn',
    'enterFn',
    'descriptionFn',
  ],
  emits: ['fileUpdated', 'previewFile', 'downloadFile', 'removeFile', 'titleClick'],
  template: `
    <div class="exhibit-list-stub" :data-editable="String(alwaysEditable)" :data-linkable="String(linkableTitle)">
      <button class="stub-title" @click="$emit('titleClick', entries[0].file)">title</button>
      <button
        class="stub-updated"
        @click="$emit('fileUpdated', { ...entries[0].file, markedValue: 'Z', status: 'Marked' })"
      >upd</button>
      <span v-for="e in entries" :key="e.file.id" class="stub-entry">
        {{ e.file.originalFileName }}:{{ e.file.markedValue ?? '-' }}
      </span>
    </div>`,
};

function mountSearch() {
  const pinia = createPinia();
  setActivePinia(pinia);
  return mount(ExhibitSearch, {
    global: {
      plugins: [pinia],
      stubs: {
        ExhibitList: ExhibitListStub,
        ExhibitDetailModal: {
          props: ['result'],
          emits: ['close'],
          template: '<div class="detail-modal-stub" />',
        },
        FileViewer: true,
      },
    },
  });
}

const fileNumberInput = (wrapper: ReturnType<typeof mountSearch>) =>
  wrapper.find('input[placeholder="e.g. AH123456789-1"]');
const lastNameInput = (wrapper: ReturnType<typeof mountSearch>) =>
  wrapper.find('input[placeholder="last name"]');

beforeEach(() => {
  localStorage.clear();
  setActivePinia(createPinia());
  mockSearchExhibits.mockReset();
});

describe('ExhibitSearch', () => {
  it('blocks search when no valid term is present and shows a hint', () => {
    const wrapper = mountSearch();

    const searchBtn = wrapper.find('.btn-search');
    expect(searchBtn.attributes('disabled')).toBeDefined();
    expect(wrapper.find('.validation-hint').exists()).toBe(true);
  });

  it('blocks search when the file number is shorter than the minimum', async () => {
    const wrapper = mountSearch();
    await fileNumberInput(wrapper).setValue('AH1');

    expect(wrapper.find('.btn-search').attributes('disabled')).toBeDefined();
    expect(wrapper.find('.validation-hint').text()).toContain('at least 5');
  });

  it('enables search when a last name is entered', async () => {
    const wrapper = mountSearch();
    await lastNameInput(wrapper).setValue('Smith');

    expect(wrapper.find('.btn-search').attributes('disabled')).toBeUndefined();
  });

  it('maps results into ExhibitList entries preserving backend order', async () => {
    mockSearchExhibits.mockResolvedValue([
      makeResult({ file: makeFile({ id: 'f1', originalFileName: 'first.pdf' }) }),
      makeResult({ file: makeFile({ id: 'f2', originalFileName: 'second.pdf' }) }),
    ]);
    const wrapper = mountSearch();
    await lastNameInput(wrapper).setValue('Smith');

    await wrapper.find('.filter-panel').trigger('submit');
    await flushPromises();

    const entries = wrapper.findAll('.stub-entry');
    expect(entries).toHaveLength(2);
    expect(entries[0].text()).toContain('first.pdf');
    expect(entries[1].text()).toContain('second.pdf');
  });

  it('passes always-editable and linkable-title to ExhibitList', async () => {
    mockSearchExhibits.mockResolvedValue([makeResult()]);
    const wrapper = mountSearch();
    await lastNameInput(wrapper).setValue('Smith');
    await wrapper.find('.filter-panel').trigger('submit');
    await flushPromises();

    const stub = wrapper.find('.exhibit-list-stub');
    expect(stub.attributes('data-editable')).toBe('true');
    expect(stub.attributes('data-linkable')).toBe('true');
  });

  it('patches the row in place when ExhibitList emits file-updated', async () => {
    mockSearchExhibits.mockResolvedValue([makeResult({ file: makeFile({ id: 'file-1' }) })]);
    const wrapper = mountSearch();
    await lastNameInput(wrapper).setValue('Smith');
    await wrapper.find('.filter-panel').trigger('submit');
    await flushPromises();

    await wrapper.find('.stub-updated').trigger('click');

    expect(wrapper.find('.stub-entry').text()).toContain('exhibit-a.pdf:Z');
  });

  it('opens the detail modal when ExhibitList emits title-click', async () => {
    mockSearchExhibits.mockResolvedValue([makeResult()]);
    const wrapper = mountSearch();
    await lastNameInput(wrapper).setValue('Smith');
    await wrapper.find('.filter-panel').trigger('submit');
    await flushPromises();

    expect(wrapper.find('.detail-modal-stub').exists()).toBe(false);

    await wrapper.find('.stub-title').trigger('click');

    expect(wrapper.find('.detail-modal-stub').exists()).toBe(true);
  });

  it('shows an empty state when the search returns no results', async () => {
    mockSearchExhibits.mockResolvedValue([]);
    const wrapper = mountSearch();
    await lastNameInput(wrapper).setValue('Smith');
    await wrapper.find('.filter-panel').trigger('submit');
    await flushPromises();

    expect(wrapper.find('.empty-state').exists()).toBe(true);
  });

  it('shows a permission error when the search returns 403', async () => {
    mockSearchExhibits.mockRejectedValue({ isAxiosError: true, response: { status: 403 } });
    const wrapper = mountSearch();
    await lastNameInput(wrapper).setValue('Smith');
    await wrapper.find('.filter-panel').trigger('submit');
    await flushPromises();

    expect(wrapper.find('.alert-danger').text()).toContain('permission');
  });
});
