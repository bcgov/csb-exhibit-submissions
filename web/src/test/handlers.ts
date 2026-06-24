import { http, HttpResponse } from 'msw'

export const handlers = [
  http.post('/api/auth/login', () => HttpResponse.json({ token: '<test-jwt>' })),
  http.get('/api/submissions/listing', () =>
    HttpResponse.json({ items: [], totalCount: 0, page: 1, pageSize: 20 }),
  ),
  http.post('/api/submissions/submit', () => HttpResponse.json({ success: true })),
  http.get('/api/location/getLocations', () => HttpResponse.json([])),
]
