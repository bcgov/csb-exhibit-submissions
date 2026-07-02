import { mount, flushPromises } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import SubmissionListing from '@/components/admin/SubmissionListing.vue';
import type { PagedResult, SubmissionReviewModel } from '@/models/SubmissionReviewModel';

const mockPush = vi.hoisted(() => vi.fn());
vi.mock('vue-router', () => ({
  useRouter: () => ({ push: mockPush }),
}));

const mockRetrieveSubmissionListing = vi.hoisted(() => vi.fn());
vi.mock('@/services/SubmissionService', () => ({
  default: () => ({
    retrieveSubmissionListing: mockRetrieveSubmissionListing,
  }),
}));

const makeItem = (overrides: Partial<SubmissionReviewModel> = {}): SubmissionReviewModel => ({
  id: 1,
  courtDateTime: '2026-06-01T10:00:00',
  location: 'Test Court',
  room: 'ROOM1',
  locationName: 'Test Court',
  status: 'Pending',
  exhibitCount: 3,
  tickets: [
    {
      appearanceId: 'A1',
      fileNumberText: 'FILE001',
      accusedName: 'Smith, John',
      appearanceDateTime: '2026-06-01T10:00:00',
      appearanceSequenceNumber: '001',
      appearanceReasonCode: 'ADP',
      courtListType: 'Criminal',
      accusedDOB: '1980-01-01',
    },
  ],
  files: [],
  ...overrides,
});

const makePagedResult = (
  items: SubmissionReviewModel[],
  totalCount?: number,
): PagedResult<SubmissionReviewModel> => ({
  items,
  totalCount: totalCount ?? items.length,
  page: 1,
  pageSize: 20,
});

function mountListing() {
  const pinia = createPinia();
  setActivePinia(pinia);
  return mount(SubmissionListing, { global: { plugins: [pinia] } });
}

beforeEach(() => {
  localStorage.clear();
  setActivePinia(createPinia());
  mockPush.mockClear();
  mockRetrieveSubmissionListing.mockReset();
});

describe('SubmissionListing', () => {
  it('calls retrieveSubmissionListing on mount and renders rows', async () => {
    mockRetrieveSubmissionListing.mockResolvedValue(makePagedResult([makeItem()]));
    const wrapper = mountListing();
    await flushPromises();

    expect(mockRetrieveSubmissionListing).toHaveBeenCalledTimes(1);
    expect(wrapper.find('tbody').findAll('tr')).toHaveLength(1);
  });

  it('displays exhibit count in the table', async () => {
    mockRetrieveSubmissionListing.mockResolvedValue(
      makePagedResult([makeItem({ exhibitCount: 5 })]),
    );
    const wrapper = mountListing();
    await flushPromises();

    expect(wrapper.text()).toContain('5');
  });

  it('renders Pending status chip with correct class', async () => {
    mockRetrieveSubmissionListing.mockResolvedValue(
      makePagedResult([makeItem({ status: 'Pending' })]),
    );
    const wrapper = mountListing();
    await flushPromises();

    const chip = wrapper.find('.status-chip');
    expect(chip.classes()).toContain('status-pending');
    expect(chip.text()).toBe('Pending');
  });

  it('renders Accepted status chip with correct class', async () => {
    mockRetrieveSubmissionListing.mockResolvedValue(
      makePagedResult([makeItem({ status: 'Accepted' })]),
    );
    const wrapper = mountListing();
    await flushPromises();

    const chip = wrapper.find('.status-chip');
    expect(chip.classes()).toContain('status-accepted');
    expect(chip.text()).toBe('Accepted');
  });

  it('renders Rejected status chip with correct class', async () => {
    mockRetrieveSubmissionListing.mockResolvedValue(
      makePagedResult([makeItem({ status: 'Rejected' })]),
    );
    const wrapper = mountListing();
    await flushPromises();

    const chip = wrapper.find('.status-chip');
    expect(chip.classes()).toContain('status-rejected');
    expect(chip.text()).toBe('Rejected');
  });

  it('renders all submissions including Accepted (historical view)', async () => {
    const items = [
      makeItem({ id: 1, status: 'Pending' }),
      makeItem({ id: 2, status: 'Accepted' }),
      makeItem({ id: 3, status: 'Rejected' }),
    ];
    mockRetrieveSubmissionListing.mockResolvedValue(makePagedResult(items, 3));
    const wrapper = mountListing();
    await flushPromises();

    expect(wrapper.find('tbody').findAll('tr')).toHaveLength(3);
  });

  it('shows pagination when totalCount exceeds one page', async () => {
    const items = Array.from({ length: 20 }, (_, i) => makeItem({ id: i + 1 }));
    mockRetrieveSubmissionListing.mockResolvedValue({
      items,
      totalCount: 45,
      page: 1,
      pageSize: 20,
    });
    const wrapper = mountListing();
    await flushPromises();

    expect(wrapper.find('.pagination').exists()).toBe(true);
    expect(wrapper.text()).toContain('45 total');
  });

  it('does not show pagination when totalCount fits on one page', async () => {
    mockRetrieveSubmissionListing.mockResolvedValue(makePagedResult([makeItem()]));
    const wrapper = mountListing();
    await flushPromises();

    expect(wrapper.find('.pagination').exists()).toBe(false);
  });

  it('Previous button is disabled on first page', async () => {
    const items = Array.from({ length: 20 }, (_, i) => makeItem({ id: i + 1 }));
    mockRetrieveSubmissionListing.mockResolvedValue({
      items,
      totalCount: 45,
      page: 1,
      pageSize: 20,
    });
    const wrapper = mountListing();
    await flushPromises();

    const prevBtn = wrapper.find('.pagination button:first-child');
    expect(prevBtn.attributes('disabled')).toBeDefined();
  });

  it('Apply button resets to page 1 and refetches', async () => {
    mockRetrieveSubmissionListing.mockResolvedValue(makePagedResult([makeItem()]));
    const wrapper = mountListing();
    await flushPromises();
    mockRetrieveSubmissionListing.mockClear();

    await wrapper.find('.btn-apply').trigger('click');
    await flushPromises();

    expect(mockRetrieveSubmissionListing).toHaveBeenCalledTimes(1);
    expect(mockRetrieveSubmissionListing).toHaveBeenCalledWith(
      expect.objectContaining({ page: 1 }),
    );
  });

  it('Clear button resets filter fields and refetches', async () => {
    mockRetrieveSubmissionListing.mockResolvedValue(makePagedResult([makeItem()]));
    const wrapper = mountListing();
    await flushPromises();

    const fileNumInput = wrapper.find('input[placeholder="e.g. FILE001"]');
    await fileNumInput.setValue('FILE999');

    mockRetrieveSubmissionListing.mockClear();
    await wrapper.find('.btn-clear').trigger('click');
    await flushPromises();

    expect(mockRetrieveSubmissionListing).toHaveBeenCalledTimes(1);
    expect(mockRetrieveSubmissionListing).toHaveBeenCalledWith(
      expect.objectContaining({ fileNumberText: '', page: 1 }),
    );
  });

  it('passes status filter to listing call', async () => {
    mockRetrieveSubmissionListing.mockResolvedValue(makePagedResult([]));
    const wrapper = mountListing();
    await flushPromises();
    mockRetrieveSubmissionListing.mockClear();

    const statusSelect = wrapper.find('select');
    await statusSelect.setValue('Accepted');
    await wrapper.find('.btn-apply').trigger('click');
    await flushPromises();

    expect(mockRetrieveSubmissionListing).toHaveBeenCalledWith(
      expect.objectContaining({ status: 'Accepted' }),
    );
  });

  it('double-clicking a row navigates to review page', async () => {
    mockRetrieveSubmissionListing.mockResolvedValue(makePagedResult([makeItem({ id: 7 })]));
    const wrapper = mountListing();
    await flushPromises();

    await wrapper.find('tbody tr').trigger('dblclick');

    expect(mockPush).toHaveBeenCalledWith('/admin/view/7');
  });

  it('shows empty table when no submissions returned', async () => {
    mockRetrieveSubmissionListing.mockResolvedValue(makePagedResult([]));
    const wrapper = mountListing();
    await flushPromises();

    expect(wrapper.find('tbody').findAll('tr')).toHaveLength(0);
  });
});
