import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/setup'
import LoginView from '@/views/LoginView.vue'

vi.mock('@/router', () => ({
  default: {
    push: vi.fn(),
    currentRoute: { value: { path: '/', query: {} } },
  },
}))

function buildValidToken(): string {
  const encode = (obj: unknown) => Buffer.from(JSON.stringify(obj)).toString('base64url')
  return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode({
    sub: 'admin@gov.bc.ca',
    email: 'admin@gov.bc.ca',
    role: 'Admin',
    exp: Math.floor(Date.now() / 1000) + 7200,
  })}.sig`
}

beforeEach(() => {
  localStorage.clear()
  setActivePinia(createPinia())
})

describe('LoginView', () => {
  it('renders username and password fields', () => {
    const wrapper = mount(LoginView, {
      global: { plugins: [createPinia()] },
    })

    expect(wrapper.find('input[type="email"]').exists()).toBe(true)
    expect(wrapper.find('input[type="password"]').exists()).toBe(true)
  })

  it('shows error message on login failure', async () => {
    server.use(http.post('/api/auth/login', () => HttpResponse.json({}, { status: 401 })))

    const wrapper = mount(LoginView, {
      global: { plugins: [createPinia()] },
    })

    await wrapper.find('input[type="email"]').setValue('bad@user.com')
    await wrapper.find('input[type="password"]').setValue('wrong')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.find('.alert-danger').exists()).toBe(true)
    expect(wrapper.find('.alert-danger').text()).toContain('Invalid email or password')
  })

  it('redirects to home on successful login', async () => {
    const routerMock = await import('@/router')
    server.use(
      http.post('/api/auth/login', () => HttpResponse.json({ token: buildValidToken() })),
    )

    const wrapper = mount(LoginView, {
      global: { plugins: [createPinia()] },
    })

    await wrapper.find('input[type="email"]').setValue('admin@gov.bc.ca')
    await wrapper.find('input[type="password"]').setValue('pass123')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(routerMock.default.push).toHaveBeenCalled()
  })
})
