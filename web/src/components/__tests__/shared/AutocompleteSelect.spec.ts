import { mount } from '@vue/test-utils'
import AutocompleteSelect from '@/components/shared/AutocompleteSelect.vue'

interface Item {
  id: number
  label: string
}

const items: Item[] = [
  { id: 1, label: 'Alpha' },
  { id: 2, label: 'Beta' },
  { id: 3, label: 'Gamma' },
]

const defaultProps = {
  modelValue: null as Item | null,
  items,
  getLabel: (item: Item) => item.label,
  getKey: (item: Item) => item.id,
  placeholder: 'Select an item',
  label: 'Test Label',
}

describe('AutocompleteSelect', () => {
  it('renders with provided items', async () => {
    const wrapper = mount(AutocompleteSelect, { props: defaultProps })

    const input = wrapper.find('input')
    await input.trigger('focus')

    const options = wrapper.findAll('.dropdown-item')
    expect(options).toHaveLength(3)
    expect(options[0].text()).toBe('Alpha')
    expect(options[1].text()).toBe('Beta')
  })

  it('emits update:modelValue on selection', async () => {
    const wrapper = mount(AutocompleteSelect, { props: defaultProps })

    await wrapper.find('input').trigger('focus')
    await wrapper.findAll('.dropdown-item')[1].trigger('click')

    const emitted = wrapper.emitted('update:modelValue')
    expect(emitted).toBeTruthy()
    expect((emitted![0][0] as Item).label).toBe('Beta')
  })

  it('displays placeholder text when empty', () => {
    const wrapper = mount(AutocompleteSelect, { props: defaultProps })

    const input = wrapper.find('input')
    expect(input.attributes('placeholder')).toBe('Select an item')
  })
})
