import { http, HttpResponse } from 'msw'

export const handlers = [
  http.post('/api/auth/login', () => HttpResponse.json({ token: '<test-jwt>' })),
  http.get('/api/submissions/listing', () => HttpResponse.json([])),
  http.post('/api/submissions/submit', () => HttpResponse.json({ success: true })),
  http.get('/api/location/getLocations', () => HttpResponse.json([])),
]
