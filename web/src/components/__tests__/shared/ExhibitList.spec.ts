import { flushPromises, mount } from '@vue/test-utils';
import ExhibitList from '@/components/shared/ExhibitList.vue';
import { DESCRIPTION_PREVIEW_MAX_LENGTH } from '@/constants/classification';
import type { ExhibitDescriptionModel } from '@/models/ExhibitDescriptionModel';
import type { SubmissionFile } from '@/models/SubmissionReviewModel';

const makeDescription = (
  overrides: Partial<ExhibitDescriptionModel> = {},
): ExhibitDescriptionModel => ({
  id: 1,
  descriptionText: 'the first description',
  createdBy: 'officer@test.ca',
  createdAtUTC: '2026-07-01T10:00:00Z',
  ...overrides,
});

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
  descriptions: [],
  ...overrides,
});

const baseProps = () => ({
  entries: [{ file: makeFile(), fileNumbers: [] as string[] }],
  markFn: vi.fn(),
  enterFn: vi.fn(),
  addDescriptionFn: vi.fn(),
  evidenceSourceFn: vi.fn(),
});

// Expanded is the state most assertions care about; rows start condensed by default.
const expandedProps = () => ({ ...baseProps(), initialExpanded: true });

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

describe('ExhibitList — shared first row (CES-42)', () => {
  it('shows the status chip and no history button, in both states', async () => {
    const entries = [{ file: makeFile({ status: 'Entered', enteredValue: '3' }), fileNumbers: [] }];

    const condensed = mount(ExhibitList, { props: { ...baseProps(), entries } });
    expect(condensed.find('.history-btn').exists()).toBe(false);
    expect(condensed.find('.chip').text()).toBe('Exhibit 3');

    await condensed.find('.chevron-btn').trigger('click');
    expect(condensed.find('.history-btn').exists()).toBe(false);
    expect(condensed.find('.chip').text()).toBe('Exhibit 3');
  });
});

describe('ExhibitList — condensed rows (CES-42)', () => {
  it('puts the description on its own line when condensed, and in row 2 when expanded', async () => {
    const wrapper = mount(ExhibitList, { props: baseProps() });

    expect(wrapper.find('.prior-file-desc-line').exists()).toBe(true);
    expect(wrapper.find('.prior-file-row2').exists()).toBe(false);

    await wrapper.find('.chevron-btn').trigger('click');

    expect(wrapper.find('.prior-file-desc-line').exists()).toBe(false);
    expect(wrapper.find('.prior-file-row2 .description-cell').exists()).toBe(true);
  });

  it('hides the classification controls until the chevron is clicked', async () => {
    const wrapper = mount(ExhibitList, { props: baseProps() });

    expect(wrapper.find('.prior-file-row2').exists()).toBe(false);
    expect(wrapper.find('.prior-file-item--condensed').exists()).toBe(true);
    const chevron = wrapper.find('.chevron-btn');
    expect(chevron.attributes('aria-expanded')).toBe('false');

    await chevron.trigger('click');

    expect(wrapper.find('.prior-file-row2').exists()).toBe(true);
    expect(wrapper.find('.prior-file-item--condensed').exists()).toBe(false);
    expect(wrapper.find('.chevron-btn').attributes('aria-expanded')).toBe('true');
  });

  it('starts expanded when initialExpanded is set', () => {
    const wrapper = mount(ExhibitList, { props: expandedProps() });

    expect(wrapper.find('.prior-file-row2').exists()).toBe(true);
  });

  it('toggles one row without affecting its siblings', async () => {
    const wrapper = mount(ExhibitList, {
      props: {
        ...baseProps(),
        entries: [
          { file: makeFile(), fileNumbers: [] },
          { file: makeFile({ id: 'file-uuid-2' }), fileNumbers: [] },
        ],
      },
    });

    await wrapper.findAll('.chevron-btn')[0].trigger('click');

    const items = wrapper.findAll('.prior-file-item');
    expect(items[0].find('.prior-file-row2').exists()).toBe(true);
    expect(items[1].find('.prior-file-row2').exists()).toBe(false);
  });
});

describe('ExhibitList — description entries (CES-42)', () => {
  it('offers an input when the exhibit has no description, and saves it on blur', async () => {
    const props = baseProps();
    props.addDescriptionFn.mockResolvedValue(
      makeFile({ descriptions: [makeDescription({ descriptionText: 'a new description' })] }),
    );
    const wrapper = mount(ExhibitList, { props });

    const input = wrapper.find('[data-test="desc-input"]');
    expect(input.exists()).toBe(true);

    await input.setValue('a new description');
    await input.trigger('blur');

    expect(props.addDescriptionFn).toHaveBeenCalledWith('file-uuid-1', 'a new description');
    await flushPromises();
    expect(wrapper.emitted('fileUpdated')).toHaveLength(1);
  });

  it('does not save an empty description on blur', async () => {
    const props = baseProps();
    const wrapper = mount(ExhibitList, { props });

    await wrapper.find('[data-test="desc-input"]').trigger('blur');

    expect(props.addDescriptionFn).not.toHaveBeenCalled();
  });

  it('is read-only once a description exists — no input anywhere in the row', () => {
    const entries = [{ file: makeFile({ descriptions: [makeDescription()] }), fileNumbers: [] }];

    const condensed = mount(ExhibitList, { props: { ...baseProps(), entries } });
    expect(condensed.find('[data-test="desc-input"]').exists()).toBe(false);
    expect(condensed.find('[data-test="desc-preview"]').text()).toBe('the first description');

    const expanded = mount(ExhibitList, { props: { ...expandedProps(), entries } });
    expect(expanded.find('[data-test="desc-input"]').exists()).toBe(false);
    expect(expanded.find('[data-test="desc-full"]').text()).toBe('the first description');
  });

  it('truncates the condensed preview and collapses its whitespace', () => {
    const longText = 'x'.repeat(DESCRIPTION_PREVIEW_MAX_LENGTH + 50);
    const wrapper = mount(ExhibitList, {
      props: {
        ...baseProps(),
        entries: [
          {
            file: makeFile({ descriptions: [makeDescription({ descriptionText: longText })] }),
            fileNumbers: [],
          },
        ],
      },
    });

    const preview = wrapper.find('[data-test="desc-preview"]').text();
    expect(preview).toBe(`${'x'.repeat(DESCRIPTION_PREVIEW_MAX_LENGTH)}…`);
    // The full text stays available on hover.
    expect(wrapper.find('[data-test="desc-preview"]').attributes('title')).toBe(longText);
  });

  it('shows only the first entry, with a count of the addenda', () => {
    const wrapper = mount(ExhibitList, {
      props: {
        ...baseProps(),
        entries: [
          {
            file: makeFile({
              descriptions: [
                makeDescription(),
                makeDescription({ id: 2, descriptionText: 'an addendum' }),
                makeDescription({ id: 3, descriptionText: 'another addendum' }),
              ],
            }),
            fileNumbers: [],
          },
        ],
      },
    });

    expect(wrapper.find('[data-test="desc-preview"]').text()).toBe('the first description');
    expect(wrapper.find('[data-test="desc-addenda"]').text()).toContain('+2');
    expect(wrapper.text()).not.toContain('an addendum');
  });

  it('disables the input once the exhibit is Entered (officer view)', () => {
    const wrapper = mount(ExhibitList, {
      props: {
        ...expandedProps(),
        entries: [{ file: makeFile({ status: 'Entered', enteredValue: '3' }), fileNumbers: [] }],
      },
    });

    expect(wrapper.find('[data-test="desc-input"]').attributes('disabled')).toBeDefined();
  });

  it('keeps the input enabled for an Entered exhibit in alwaysEditable (admin) mode', () => {
    const wrapper = mount(ExhibitList, {
      props: {
        ...expandedProps(),
        alwaysEditable: true,
        entries: [{ file: makeFile({ status: 'Entered', enteredValue: '3' }), fileNumbers: [] }],
      },
    });

    expect(wrapper.find('[data-test="desc-input"]').attributes('disabled')).toBeUndefined();
  });
});

describe('ExhibitList — evidence source dropdown', () => {
  const sourceSelect = (wrapper: ReturnType<typeof mount>) =>
    wrapper.find('.source-group select');

  it('renders a blank option plus the three source types', () => {
    const wrapper = mount(ExhibitList, { props: expandedProps() });

    const options = sourceSelect(wrapper).findAll('option');
    expect(options.map((o) => o.element.value)).toEqual(['', 'BodyCam', 'DashCam', 'Other']);
    expect(options.map((o) => o.text())).toEqual(['—', 'Body Cam', 'Dash Cam', 'Other']);
  });

  it('reflects the current value and defaults to blank when unset', () => {
    const unset = mount(ExhibitList, { props: expandedProps() });
    expect((sourceSelect(unset).element as HTMLSelectElement).value).toBe('');

    const set = mount(ExhibitList, {
      props: {
        ...expandedProps(),
        entries: [{ file: makeFile({ evidenceSourceType: 'DashCam' }), fileNumbers: [] }],
      },
    });
    expect((sourceSelect(set).element as HTMLSelectElement).value).toBe('DashCam');
  });

  it('calls evidenceSourceFn with the selected value on change', async () => {
    const props = expandedProps();
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
        ...expandedProps(),
        entries: [{ file: makeFile({ status: 'Entered', enteredValue: '3' }), fileNumbers: [] }],
      },
    });

    expect(sourceSelect(wrapper).attributes('disabled')).toBeDefined();
  });

  it('stays enabled for an Entered exhibit in alwaysEditable (admin) mode', () => {
    const wrapper = mount(ExhibitList, {
      props: {
        ...expandedProps(),
        alwaysEditable: true,
        entries: [{ file: makeFile({ status: 'Entered', enteredValue: '3' }), fileNumbers: [] }],
      },
    });

    expect(sourceSelect(wrapper).attributes('disabled')).toBeUndefined();
  });
});
