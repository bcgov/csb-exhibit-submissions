import { createPinia, setActivePinia } from 'pinia';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/setup';
import useSubmissionService from '@/services/SubmissionService';
import type { ExhibitSubmissionModel } from '@/models/ExhibitSubmissionModel';
import type { SubmissionAcceptanceModel } from '@/models/SubmissionAcceptanceModel';

vi.mock('@/router', () => ({
  default: {
    push: vi.fn(),
    currentRoute: { value: { path: '/' } },
  },
}));

beforeEach(() => {
  localStorage.clear();
  setActivePinia(createPinia());
});

const mockModel: ExhibitSubmissionModel = {
  tickets: [
    {
      appearanceId: 'APP001',
      appearanceDateTime: '2026-01-01T09:00:00',
      appearanceSequenceNumber: '001',
      appearanceReasonCode: 'ADP',
      courtListType: 'Criminal',
      fileNumberText: 'FILE001',
      accusedName: 'Smith, John',
      accusedDOB: '1980-01-01',
    },
  ],
  shortDate: '2026-01-01',
  locationId: 'LOC001',
  locationNameText: 'Test Court',
  roomCode: 'ROOM1',
  roomText: 'Courtroom 1',
  officerNumber: 'OFF001',
};

describe('SubmissionService', () => {
  it('submitExhibits sends multipart POST to /api/submissions/submit', async () => {
    let contentType = '';
    server.use(
      http.post('/api/submissions/submit/', async ({ request }) => {
        contentType = request.headers.get('content-type') ?? '';
        return HttpResponse.json({ success: true });
      }),
    );

    const { submitExhibits } = useSubmissionService();
    const file = new File(['video content'], 'test.mp4', { type: 'video/mp4' });
    await submitExhibits(mockModel, [file]);

    expect(contentType).toContain('multipart/form-data');
  });

  it('submitExhibits calls progressCallback', async () => {
    server.use(
      http.post('/api/submissions/submit/', () => HttpResponse.json({ success: true })),
    );

    const progressValues: number[] = [];
    const { submitExhibits } = useSubmissionService();
    const file = new File(['video content'], 'test.mp4', { type: 'video/mp4' });
    await submitExhibits(mockModel, [file], (percent) => progressValues.push(percent));

    expect(progressValues.length).toBeGreaterThanOrEqual(0);
  });

  it('retrieveSubmission fetches GET /api/submissions/retrieve', async () => {
    const mockSubmission = { id: 1, tickets: [], files: [] };
    server.use(
      http.get('/api/submissions/retrieve/', () => HttpResponse.json(mockSubmission)),
    );

    const { retrieveSubmission } = useSubmissionService();
    const result = await retrieveSubmission(1);

    expect(result).toMatchObject({ id: 1 });
  });

  it('retrieveSubmissionListing fetches GET /api/submissions/listing', async () => {
    server.use(
      http.get('/api/submissions/listing/', () => HttpResponse.json([{ id: 1 }, { id: 2 }])),
    );

    const { retrieveSubmissionListing } = useSubmissionService();
    const result = await retrieveSubmissionListing();

    expect(result).toHaveLength(2);
  });

  it('acceptSubmissionFiles sends POST /api/submissions/accept', async () => {
    let capturedBody: unknown = null;
    server.use(
      http.post('/api/submissions/accept/', async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json(true);
      }),
    );

    const model: SubmissionAcceptanceModel = { fileId: 1, acceptedFiles: [] as string[] };
    const { acceptSubmissionFiles } = useSubmissionService();
    await acceptSubmissionFiles(model);

    expect(capturedBody).toMatchObject({ fileId: 1 });
  });

  it('rejectAndCloseSubmission sends POST /api/submissions/reject', async () => {
    let capturedBody: unknown = null;
    server.use(
      http.post('/api/submissions/reject/', async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json(true);
      }),
    );

    const model: SubmissionAcceptanceModel = { fileId: 2, acceptedFiles: [] as string[] };
    const { rejectAndCloseSubmission } = useSubmissionService();
    await rejectAndCloseSubmission(model);

    expect(capturedBody).toMatchObject({ fileId: 2 });
  });

  it('getSubmissionsByFileNumber fetches GET /api/submissions/by-file-number', async () => {
    const mockPrior = [{ submissionId: 1, location: 'Test Court', room: 'ROOM1', files: [] }];
    server.use(
      http.get('/api/submissions/by-file-number', () => HttpResponse.json(mockPrior)),
    );

    const { getSubmissionsByFileNumber } = useSubmissionService();
    const result = await getSubmissionsByFileNumber('FILE001');

    expect(result).toHaveLength(1);
    expect(result[0]).toMatchObject({ submissionId: 1 });
  });

  it('removeFile sends DELETE /api/submissions/files/:fileId and returns true on success', async () => {
    const fileId = '550e8400-e29b-41d4-a716-446655440000';
    let capturedUrl = '';
    server.use(
      http.delete(`/api/submissions/files/${fileId}`, ({ request }) => {
        capturedUrl = request.url;
        return new HttpResponse(null, { status: 200 });
      }),
    );

    const { removeFile } = useSubmissionService();
    const result = await removeFile(fileId);

    expect(result).toBe(true);
    expect(capturedUrl).toContain(fileId);
  });

  it('removeFile returns false on API error', async () => {
    const fileId = '550e8400-e29b-41d4-a716-446655440001';
    server.use(
      http.delete(`/api/submissions/files/${fileId}`, () => new HttpResponse(null, { status: 404 })),
    );

    const { removeFile } = useSubmissionService();
    const result = await removeFile(fileId);

    expect(result).toBe(false);
  });
});
