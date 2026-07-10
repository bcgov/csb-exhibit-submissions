import { flushPromises, mount } from '@vue/test-utils';
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
  evidenceSourceFn: vi.fn(),
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

describe('ExhibitList — evidence source dropdown', () => {
  const sourceSelect = (wrapper: ReturnType<typeof mount>) =>
    wrapper.find('.source-group select');

  it('renders a blank option plus the three source types', () => {
    const wrapper = mount(ExhibitList, { props: baseProps() });

    const options = sourceSelect(wrapper).findAll('option');
    expect(options.map((o) => o.element.value)).toEqual(['', 'BodyCam', 'DashCam', 'Other']);
    expect(options.map((o) => o.text())).toEqual(['—', 'Body Cam', 'Dash Cam', 'Other']);
  });

  it('reflects the current value and defaults to blank when unset', () => {
    const unset = mount(ExhibitList, { props: baseProps() });
    expect((sourceSelect(unset).element as HTMLSelectElement).value).toBe('');

    const set = mount(ExhibitList, {
      props: {
        ...baseProps(),
        entries: [{ file: makeFile({ evidenceSourceType: 'DashCam' }), fileNumbers: [] }],
      },
    });
    expect((sourceSelect(set).element as HTMLSelectElement).value).toBe('DashCam');
  });

  it('calls evidenceSourceFn with the selected value on change', async () => {
    const props = baseProps();
    props.evidenceSourceFn.mockResolvedValue(makeFile({ evidenceSourceType: 'BodyCam' }));
    const wrapper = mount(ExhibitList, { props });

    await sourceSelect(wrapper).setValue('BodyCam');

    expect(props.evidenceSourceFn).toHaveBeenCalledWith('file-uuid-1', 'BodyCam');
    await flushPromises();
    expect(wrapper.emitted('fileUpdated')).toHaveLength(1);
  });

  it('is disabled once the exhibit is Entered (officer view)', () => {
    const wrapper = mount(ExhibitList, {
      props: {
        ...baseProps(),
        entries: [{ file: makeFile({ status: 'Entered', enteredValue: '3' }), fileNumbers: [] }],
      },
    });

    expect(sourceSelect(wrapper).attributes('disabled')).toBeDefined();
  });

  it('stays enabled for an Entered exhibit in alwaysEditable (admin) mode', () => {
    const wrapper = mount(ExhibitList, {
      props: {
        ...baseProps(),
        alwaysEditable: true,
        entries: [{ file: makeFile({ status: 'Entered', enteredValue: '3' }), fileNumbers: [] }],
      },
    });

    expect(sourceSelect(wrapper).attributes('disabled')).toBeUndefined();
  });
});
