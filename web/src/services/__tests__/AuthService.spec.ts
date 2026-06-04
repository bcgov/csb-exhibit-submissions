import { createPinia, setActivePinia } from 'pinia'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/setup'
import { useAuthStore } from '@/stores/authStore'
import useAuthService from '@/services/AuthService'

vi.mock('@/router', () => ({
  default: {
    push: vi.fn(),
    currentRoute: { value: { path: '/' } },
  },
}))

beforeEach(() => {
  localStorage.clear()
  setActivePinia(createPinia())
})

describe('AuthService', () => {
  it('login calls POST /api/auth/login with credentials', async () => {
    let capturedBody: unknown = null
    server.use(
      http.post('/api/auth/login', async ({ request }) => {
        capturedBody = await request.json()
        return HttpResponse.json({ token: 'test-token-value' })
      }),
    )

    const { login } = useAuthService()
    await login('user@gov.bc.ca', 'pass123')

    expect(capturedBody).toMatchObject({ username: 'user@gov.bc.ca', password: 'pass123' })
  })

  it('login stores token in authStore on success', async () => {
    server.use(
      http.post('/api/auth/login', () =>
        HttpResponse.json({
          token: buildValidToken(),
        }),
      ),
    )

    const { login } = useAuthService()
    await login('user@gov.bc.ca', 'pass123')

    const authStore = useAuthStore()
    expect(authStore.token).not.toBeNull()
  })

  it('login throws on 401', async () => {
    server.use(http.post('/api/auth/login', () => HttpResponse.json({}, { status: 401 })))

    const { login } = useAuthService()
    await expect(login('bad@user.com', 'wrong')).rejects.toThrow()
  })

  it('logout clears authStore', () => {
    const authStore = useAuthStore()
    authStore.setToken('some-token')

    const { logout } = useAuthService()
    logout()

    expect(authStore.token).toBeNull()
  })
})

function buildValidToken(): string {
  const encode = (obj: unknown) => Buffer.from(JSON.stringify(obj)).toString('base64url')
  return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode({
    sub: 'user@gov.bc.ca',
    email: 'user@gov.bc.ca',
    role: 'User',
    exp: Math.floor(Date.now() / 1000) + 7200,
  })}.sig`
}
