import { mount } from '@vue/test-utils';
import ExhibitList from '@/components/shared/ExhibitList.vue';
import type { SubmissionFile } from '@/models/SubmissionReviewModel';

vi.mock('@/services/SubmissionService', () => ({
  default: () => ({
    getFileHistory: vi.fn().mockResolvedValue([]),
  }),
}));

const makeFile = (overrides: Partial<SubmissionFile> = {}): SubmissionFile => ({
  id: 'file-uuid-1',
  originalFileName: 'exhibit.mp4',
  storedFileName: 'stored.mp4',
  viewUrl: '/api/files/file-uuid-1/view',
  downloadUrl: '/api/files/file-uuid-1/download',
  contentType: 'video/mp4',
  fileSize: 1024,
  storageProvider: 'Local',
  status: 'Unclassified',
  ...overrides,
});

const baseProps = () => ({
  entries: [{ file: makeFile(), fileNumbers: [] as string[] }],
  markFn: vi.fn(),
  enterFn: vi.fn(),
  descriptionFn: vi.fn(),
});

describe('ExhibitList — linkableTitle hook', () => {
  it('renders the filename as a link-button and emits titleClick when linkableTitle is set', async () => {
    const wrapper = mount(ExhibitList, {
      props: { ...baseProps(), linkableTitle: true },
    });

    const link = wrapper.find('.prior-file-name-link');
    expect(link.exists()).toBe(true);

    await link.trigger('click');

    const emitted = wrapper.emitted('titleClick');
    expect(emitted).toHaveLength(1);
    expect((emitted![0][0] as SubmissionFile).id).toBe('file-uuid-1');
  });

  it('renders the filename as plain text (no link) by default', () => {
    const wrapper = mount(ExhibitList, { props: baseProps() });

    expect(wrapper.find('.prior-file-name-link').exists()).toBe(false);
    expect(wrapper.find('.prior-file-name').exists()).toBe(true);
    expect(wrapper.emitted('titleClick')).toBeUndefined();
  });
});
