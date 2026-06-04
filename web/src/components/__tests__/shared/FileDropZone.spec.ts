import { mount } from '@vue/test-utils'
import FileDropZone from '@/components/shared/FileDropZone.vue'

describe('FileDropZone', () => {
  it('renders drop target', () => {
    const wrapper = mount(FileDropZone)
    expect(wrapper.find('.dropzone').exists()).toBe(true)
  })

  it('emits filesChanged when files are dropped', async () => {
    const wrapper = mount(FileDropZone)
    const file = new File(['content'], 'test.mp4', { type: 'video/mp4' })

    const dropzone = wrapper.find('.dropzone')
    const dropEvent = new Event('drop', { bubbles: true })
    Object.defineProperty(dropEvent, 'dataTransfer', {
      value: { files: [file] },
    })
    Object.defineProperty(dropEvent, 'preventDefault', {
      value: vi.fn(),
    })

    await dropzone.element.dispatchEvent(dropEvent)
    await wrapper.vm.$nextTick()

    const emitted = wrapper.emitted('filesChanged')
    expect(emitted).toBeTruthy()
    expect((emitted![0][0] as File[]).length).toBe(1)
    expect((emitted![0][0] as File[])[0].name).toBe('test.mp4')
  })

  it('shows alert for exceeding max files', async () => {
    const wrapper = mount(FileDropZone)
    const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {})

    const fileInput = wrapper.find('input[type="file"]')
    const files = Array.from({ length: 11 }, (_, i) =>
      new File(['content'], `file${i}.mp4`, { type: 'video/mp4' }),
    )

    Object.defineProperty(fileInput.element, 'files', {
      value: files,
      writable: false,
    })

    await fileInput.trigger('change')

    expect(alertSpy).toHaveBeenCalledWith('Maximum 10 files allowed')
    alertSpy.mockRestore()
  })
})
