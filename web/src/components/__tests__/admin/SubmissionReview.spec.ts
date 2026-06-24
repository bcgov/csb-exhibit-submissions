import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import SubmissionReview from '@/components/admin/SubmissionReview.vue'
import type { SubmissionFile, SubmissionReviewModel } from '@/models/SubmissionReviewModel'

const mockPush = vi.hoisted(() => vi.fn())
vi.mock('vue-router', () => ({
  useRouter: () => ({ push: mockPush }),
  useRoute: () => ({ params: { id: '1' } }),
}))

const mockRetrieveSubmission = vi.hoisted(() => vi.fn())
const mockAcceptSubmission = vi.hoisted(() => vi.fn())
const mockRejectSubmission = vi.hoisted(() => vi.fn())
const mockRemoveFile = vi.hoisted(() => vi.fn())
const mockMarkExhibit = vi.hoisted(() => vi.fn())
const mockEnterExhibit = vi.hoisted(() => vi.fn())
const mockUpdateExhibitDescription = vi.hoisted(() => vi.fn())

vi.mock('@/services/SubmissionService', () => ({
  default: () => ({
    retrieveSubmission: mockRetrieveSubmission,
    acceptSubmission: mockAcceptSubmission,
    rejectSubmission: mockRejectSubmission,
    removeFile: mockRemoveFile,
    markExhibit: mockMarkExhibit,
    enterExhibit: mockEnterExhibit,
    updateExhibitDescription: mockUpdateExhibitDescription,
  }),
}))

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
})

const makeSubmission = (overrides: Partial<SubmissionReviewModel> = {}): SubmissionReviewModel => ({
  id: 1,
  courtDateTime: '2026-06-01T10:00:00',
  location: 'Test Court',
  room: 'ROOM1',
  locationName: 'Test Court',
  status: 'Pending',
  exhibitCount: 1,
  tickets: [],
  files: [],
  ...overrides,
})

function mountReview() {
  const pinia = createPinia()
  setActivePinia(pinia)
  return mount(SubmissionReview, {
    global: {
      plugins: [pinia],
      stubs: {
        AppModal: {
          template: '<div class="app-modal-stub"><slot /><button class="modal-confirm" @click="$emit(\'confirm\')">Confirm</button><button class="modal-cancel" @click="$emit(\'cancel\')">Cancel</button></div>',
          emits: ['confirm', 'cancel'],
        },
        FileViewer: { template: '<div class="file-viewer-stub" />' },
      },
    },
  })
}

beforeEach(() => {
  localStorage.clear()
  setActivePinia(createPinia())
  mockPush.mockClear()
  mockRetrieveSubmission.mockReset()
  mockAcceptSubmission.mockReset()
  mockRejectSubmission.mockReset()
  mockRemoveFile.mockReset()
  mockMarkExhibit.mockReset()
  mockEnterExhibit.mockReset()
  mockUpdateExhibitDescription.mockReset()
})

describe('SubmissionReview', () => {
  describe('status display', () => {
    it('renders Pending status chip', async () => {
      mockRetrieveSubmission.mockResolvedValue(makeSubmission({ status: 'Pending' }))
      const wrapper = mountReview()
      await flushPromises()

      expect(wrapper.find('.status-pending').exists()).toBe(true)
    })

    it('renders Accepted status chip', async () => {
      mockRetrieveSubmission.mockResolvedValue(makeSubmission({ status: 'Accepted', files: [makeFile({ enteredValue: '1' })] }))
      const wrapper = mountReview()
      await flushPromises()

      expect(wrapper.find('.status-accepted').exists()).toBe(true)
    })

    it('renders Rejected status chip', async () => {
      mockRetrieveSubmission.mockResolvedValue(makeSubmission({ status: 'Rejected' }))
      const wrapper = mountReview()
      await flushPromises()

      expect(wrapper.find('.status-rejected').exists()).toBe(true)
    })
  })

  describe('classification controls — Pending submission', () => {
    it('shows Marked select for non-Removed files', async () => {
      mockRetrieveSubmission.mockResolvedValue(
        makeSubmission({ files: [makeFile({ status: 'Unclassified' })] }),
      )
      const wrapper = mountReview()
      await flushPromises()

      expect(wrapper.find('.prior-file-row2').exists()).toBe(true)
      expect(wrapper.find('label').text()).toContain('Marked')
    })

    it('shows Entered select for non-Removed files', async () => {
      mockRetrieveSubmission.mockResolvedValue(
        makeSubmission({ files: [makeFile({ status: 'Unclassified' })] }),
      )
      const wrapper = mountReview()
      await flushPromises()

      const labels = wrapper.findAll('label').map(l => l.text())
      expect(labels.some(t => t.includes('Entered'))).toBe(true)
    })

    it('shows description input for non-Removed files', async () => {
      mockRetrieveSubmission.mockResolvedValue(
        makeSubmission({ files: [makeFile({ status: 'Unclassified' })] }),
      )
      const wrapper = mountReview()
      await flushPromises()

      expect(wrapper.find('input[type="text"]').exists()).toBe(true)
    })

    it('shows Remove button for non-Removed files', async () => {
      mockRetrieveSubmission.mockResolvedValue(
        makeSubmission({ files: [makeFile()] }),
      )
      const wrapper = mountReview()
      await flushPromises()

      expect(wrapper.find('.rm-btn').exists()).toBe(true)
    })
  })

  describe('Removed exhibits', () => {
    it('applies prior-file-item-removed class to Removed files', async () => {
      mockRetrieveSubmission.mockResolvedValue(
        makeSubmission({
          files: [makeFile({ status: 'Removed', deletedAt: '2026-06-01T12:00:00Z' })],
        }),
      )
      const wrapper = mountReview()
      await flushPromises()

      expect(wrapper.find('.prior-file-item-removed').exists()).toBe(true)
    })

    it('does not show classification controls for Removed files', async () => {
      mockRetrieveSubmission.mockResolvedValue(
        makeSubmission({
          files: [makeFile({ status: 'Removed', deletedAt: '2026-06-01T12:00:00Z' })],
        }),
      )
      const wrapper = mountReview()
      await flushPromises()

      expect(wrapper.find('.prior-file-row2').exists()).toBe(false)
    })

    it('does not show Remove button for Removed files', async () => {
      mockRetrieveSubmission.mockResolvedValue(
        makeSubmission({
          files: [makeFile({ status: 'Removed', deletedAt: '2026-06-01T12:00:00Z' })],
        }),
      )
      const wrapper = mountReview()
      await flushPromises()

      expect(wrapper.find('.rm-btn').exists()).toBe(false)
    })
  })

  describe('terminal submissions (Accepted/Rejected)', () => {
    it('disables classification controls when submission is Accepted', async () => {
      mockRetrieveSubmission.mockResolvedValue(
        makeSubmission({
          status: 'Accepted',
          files: [makeFile({ status: 'Entered', enteredValue: '1' })],
        }),
      )
      const wrapper = mountReview()
      await flushPromises()

      const row2 = wrapper.find('.prior-file-row2')
      expect(row2.exists()).toBe(true)
      expect(row2.find('select').attributes('disabled')).toBeDefined()
    })

    it('hides Accept/Reject buttons when submission is Accepted', async () => {
      mockRetrieveSubmission.mockResolvedValue(
        makeSubmission({ status: 'Accepted', files: [] }),
      )
      const wrapper = mountReview()
      await flushPromises()

      expect(wrapper.find('.actions-main').exists()).toBe(false)
    })

    it('shows View/Download for retained files on terminal submission', async () => {
      mockRetrieveSubmission.mockResolvedValue(
        makeSubmission({
          status: 'Accepted',
          files: [makeFile({ status: 'Entered', enteredValue: '1', contentType: 'video/mp4' })],
        }),
      )
      const wrapper = mountReview()
      await flushPromises()

      const actions = wrapper.find('.view-container')
      expect(actions.exists()).toBe(true)
      expect(actions.text()).toContain('Download')
    })
  })

  describe('Accept button', () => {
    it('is disabled when a file is not Entered or Removed', async () => {
      mockRetrieveSubmission.mockResolvedValue(
        makeSubmission({ files: [makeFile({ status: 'Unclassified' })] }),
      )
      const wrapper = mountReview()
      await flushPromises()

      const acceptBtn = wrapper.find('button.accept')
      expect(acceptBtn.attributes('disabled')).toBeDefined()
    })

    it('is enabled when all files are Entered', async () => {
      mockRetrieveSubmission.mockResolvedValue(
        makeSubmission({
          files: [makeFile({ status: 'Entered', enteredValue: '1' })],
        }),
      )
      const wrapper = mountReview()
      await flushPromises()

      const acceptBtn = wrapper.find('button.accept')
      expect(acceptBtn.attributes('disabled')).toBeUndefined()
    })

    it('is enabled when all files are Removed', async () => {
      mockRetrieveSubmission.mockResolvedValue(
        makeSubmission({
          files: [makeFile({ status: 'Removed', deletedAt: '2026-06-01T12:00:00Z' })],
        }),
      )
      const wrapper = mountReview()
      await flushPromises()

      const acceptBtn = wrapper.find('button.accept')
      expect(acceptBtn.attributes('disabled')).toBeUndefined()
    })

    it('is enabled when files mix Entered and Removed', async () => {
      mockRetrieveSubmission.mockResolvedValue(
        makeSubmission({
          files: [
            makeFile({ id: 'f1', status: 'Entered', enteredValue: '1' }),
            makeFile({ id: 'f2', status: 'Removed', deletedAt: '2026-06-01T12:00:00Z' }),
          ],
        }),
      )
      const wrapper = mountReview()
      await flushPromises()

      const acceptBtn = wrapper.find('button.accept')
      expect(acceptBtn.attributes('disabled')).toBeUndefined()
    })

    it('calls acceptSubmission and redirects on success', async () => {
      mockRetrieveSubmission.mockResolvedValue(
        makeSubmission({ files: [makeFile({ status: 'Entered', enteredValue: '2' })] }),
      )
      mockAcceptSubmission.mockResolvedValue(true)
      const wrapper = mountReview()
      await flushPromises()

      await wrapper.find('button.accept').trigger('click')
      await flushPromises()

      expect(mockAcceptSubmission).toHaveBeenCalledWith({ submissionId: 1 })
      expect(mockPush).toHaveBeenCalledWith('/admin/list')
    })

    it('shows error message when acceptSubmission returns false', async () => {
      mockRetrieveSubmission.mockResolvedValue(
        makeSubmission({ files: [makeFile({ status: 'Entered', enteredValue: '2' })] }),
      )
      mockAcceptSubmission.mockResolvedValue(false)
      const wrapper = mountReview()
      await flushPromises()

      await wrapper.find('button.accept').trigger('click')
      await flushPromises()

      expect(wrapper.find('.accept-error').exists()).toBe(true)
      expect(mockPush).not.toHaveBeenCalled()
    })
  })

  describe('Reject confirmation modal', () => {
    it('shows reject modal when Reject button is clicked', async () => {
      mockRetrieveSubmission.mockResolvedValue(makeSubmission())
      const wrapper = mountReview()
      await flushPromises()

      await wrapper.find('button.remove').trigger('click')

      expect(wrapper.find('.app-modal-stub').exists()).toBe(true)
    })

    it('modal contains destructive warning text', async () => {
      mockRetrieveSubmission.mockResolvedValue(makeSubmission())
      const wrapper = mountReview()
      await flushPromises()

      await wrapper.find('button.remove').trigger('click')

      expect(wrapper.find('.app-modal-stub').text()).toContain('permanently deletes')
      expect(wrapper.find('.app-modal-stub').text()).toContain('unretrievable')
    })

    it('calls rejectSubmission and redirects when modal confirmed', async () => {
      mockRetrieveSubmission.mockResolvedValue(makeSubmission())
      mockRejectSubmission.mockResolvedValue(true)
      const wrapper = mountReview()
      await flushPromises()

      await wrapper.find('button.remove').trigger('click')
      await wrapper.find('.modal-confirm').trigger('click')
      await flushPromises()

      expect(mockRejectSubmission).toHaveBeenCalledWith({ submissionId: 1 })
      expect(mockPush).toHaveBeenCalledWith('/admin/list')
    })

    it('hides modal when cancelled without calling rejectSubmission', async () => {
      mockRetrieveSubmission.mockResolvedValue(makeSubmission())
      const wrapper = mountReview()
      await flushPromises()

      await wrapper.find('button.remove').trigger('click')
      await wrapper.find('.modal-cancel').trigger('click')

      expect(mockRejectSubmission).not.toHaveBeenCalled()
      expect(wrapper.find('.app-modal-stub').exists()).toBe(false)
    })
  })

  describe('Remove exhibit', () => {
    it('marks file as Removed in the list after successful removeFile', async () => {
      mockRetrieveSubmission.mockResolvedValue(
        makeSubmission({ files: [makeFile({ id: 'file-uuid-1', status: 'Unclassified' })] }),
      )
      mockRemoveFile.mockResolvedValue(true)
      const wrapper = mountReview()
      await flushPromises()

      await wrapper.find('.rm-btn').trigger('click')
      await flushPromises()

      expect(wrapper.find('.prior-file-item-removed').exists()).toBe(true)
      expect(wrapper.find('.rm-btn').exists()).toBe(false)
    })

    it('shows remove error when removeFile fails', async () => {
      mockRetrieveSubmission.mockResolvedValue(
        makeSubmission({ files: [makeFile()] }),
      )
      mockRemoveFile.mockResolvedValue(false)
      const wrapper = mountReview()
      await flushPromises()

      await wrapper.find('.rm-btn').trigger('click')
      await flushPromises()

      expect(wrapper.find('.remove-error').exists()).toBe(true)
    })
  })
})
