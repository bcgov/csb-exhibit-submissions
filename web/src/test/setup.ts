import { setupServer } from 'msw/node'
import { handlers } from './handlers'

export const server = setupServer(...handlers)

beforeAll(() => server.listen({ onUnhandledRequest: 'warn' }))
afterEach(() => server.resetHandlers())
afterAll(() => server.close())

// Node.js 22+ ships a built-in localStorage that omits parts of the Storage
// interface (notably .clear). Stub it with a complete in-memory implementation
// so all tests can use localStorage.clear/getItem/setItem/removeItem reliably.
const _store: Record<string, string> = {}
vi.stubGlobal('localStorage', {
  getItem: (key: string): string | null => _store[key] ?? null,
  setItem: (key: string, value: string): void => { _store[key] = String(value) },
  removeItem: (key: string): void => { delete _store[key] },
  clear: (): void => { Object.keys(_store).forEach(k => delete _store[k]) },
  get length(): number { return Object.keys(_store).length },
  key: (i: number): string | null => Object.keys(_store)[i] ?? null,
})
