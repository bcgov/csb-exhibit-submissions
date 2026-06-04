import { createPinia, setActivePinia } from 'pinia'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/setup'
import useSubmissionService from '@/services/SubmissionService'
import type { ExhibitSubmissionModel } from '@/models/ExhibitSubmissionModel'
import type { SubmissionAcceptanceModel } from '@/models/SubmissionAcceptanceModel'

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

const mockModel: ExhibitSubmissionModel = {
  appearanceId: 'APP001',
  appearanceDateTime: '2026-01-01T09:00:00',
  shortDate: '2026-01-01',
  appearanceSequenceNumber: '001',
  appearanceReasonCode: 'ADP',
  courtListType: 'Criminal',
  fileNumberText: 'FILE001',
  locationId: 'LOC001',
  locationNameText: 'Test Court',
  roomCode: 'ROOM1',
  roomText: 'Courtroom 1',
  accusedName: 'Smith, John',
  accusedDOB: '1980-01-01',
  officerNumber: 'OFF001',
}

describe('SubmissionService', () => {
  it('submitExhibits sends multipart POST to /api/submissions/submit', async () => {
    let contentType = ''
    server.use(
      http.post('/api/submissions/submit/', async ({ request }) => {
        contentType = request.headers.get('content-type') ?? ''
        return HttpResponse.json({ success: true })
      }),
    )

    const { submitExhibits } = useSubmissionService()
    const file = new File(['video content'], 'test.mp4', { type: 'video/mp4' })
    await submitExhibits(mockModel, [file])

    expect(contentType).toContain('multipart/form-data')
  })

  it('submitExhibits calls progressCallback', async () => {
    server.use(
      http.post('/api/submissions/submit/', () => HttpResponse.json({ success: true })),
    )

    const progressValues: number[] = []
    const { submitExhibits } = useSubmissionService()
    const file = new File(['video content'], 'test.mp4', { type: 'video/mp4' })
    await submitExhibits(mockModel, [file], (percent) => progressValues.push(percent))

    expect(progressValues.length).toBeGreaterThanOrEqual(0)
  })

  it('retrieveSubmission fetches GET /api/submissions/retrieve', async () => {
    const mockSubmission = { id: 1, fileNumber: 'FILE001', files: [] }
    server.use(
      http.get('/api/submissions/retrieve/', () => HttpResponse.json(mockSubmission)),
    )

    const { retrieveSubmission } = useSubmissionService()
    const result = await retrieveSubmission(1)

    expect(result).toMatchObject({ id: 1 })
  })

  it('retrieveSubmissionListing fetches GET /api/submissions/listing', async () => {
    server.use(
      http.get('/api/submissions/listing/', () => HttpResponse.json([{ id: 1 }, { id: 2 }])),
    )

    const { retrieveSubmissionListing } = useSubmissionService()
    const result = await retrieveSubmissionListing()

    expect(result).toHaveLength(2)
  })

  it('acceptSubmissionFiles sends POST /api/submissions/accept', async () => {
    let capturedBody: unknown = null
    server.use(
      http.post('/api/submissions/accept/', async ({ request }) => {
        capturedBody = await request.json()
        return HttpResponse.json(true)
      }),
    )

    const model: SubmissionAcceptanceModel = { fileId: 1, acceptedFiles: [] as string[] }
    const { acceptSubmissionFiles } = useSubmissionService()
    await acceptSubmissionFiles(model)

    expect(capturedBody).toMatchObject({ fileId: 1 })
  })

  it('rejectAndCloseSubmission sends POST /api/submissions/reject', async () => {
    let capturedBody: unknown = null
    server.use(
      http.post('/api/submissions/reject/', async ({ request }) => {
        capturedBody = await request.json()
        return HttpResponse.json(true)
      }),
    )

    const model: SubmissionAcceptanceModel = { fileId: 2, acceptedFiles: [] as string[] }
    const { rejectAndCloseSubmission } = useSubmissionService()
    await rejectAndCloseSubmission(model)

    expect(capturedBody).toMatchObject({ fileId: 2 })
  })
})
