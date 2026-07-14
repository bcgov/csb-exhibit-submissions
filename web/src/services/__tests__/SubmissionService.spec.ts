import { createPinia, setActivePinia } from 'pinia';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/setup';
import useSubmissionService from '@/services/SubmissionService';
import type { ExhibitSubmissionModel } from '@/models/ExhibitSubmissionModel';
import type {
  SubmissionFile,
  SubmissionActionModel,
  PagedResult,
  SubmissionReviewModel,
} from '@/models/SubmissionReviewModel';

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
  appearanceDateTime: '2026-01-01T09:00:00',
  locationId: 'LOC001',
  locationNameText: 'Test Court',
  roomCode: 'ROOM1',
  roomText: 'Courtroom 1',
  officerNumber: 'OFF001',
};

const mockPagedResult: PagedResult<SubmissionReviewModel> = {
  items: [
    {
      id: 1,
      courtDateTime: '2026-01-01T09:00:00',
      location: 'Test Court',
      room: 'ROOM1',
      locationName: 'Test Court',
      status: 'Pending',
      exhibitCount: 2,
      tickets: [],
      files: [],
    },
  ],
  totalCount: 1,
  page: 1,
  pageSize: 20,
};

describe('SubmissionService', () => {
  it('submitExhibits sends multipart POST to /api/submissions/submit and returns the submission id', async () => {
    let contentType = '';
    server.use(
      http.post('/api/submissions/submit/', async ({ request }) => {
        contentType = request.headers.get('content-type') ?? '';
        return HttpResponse.json({ submissionId: 7 });
      }),
    );

    const { submitExhibits } = useSubmissionService();
    const file = new File(['video content'], 'test.mp4', { type: 'video/mp4' });
    const result = await submitExhibits(mockModel, [file]);

    expect(contentType).toContain('multipart/form-data');
    expect(result).toBe(7);
  });

  it('submitExhibits includes submissionId in the form when appending to an existing submission', async () => {
    let sentSubmissionId: FormDataEntryValue | null = null;
    server.use(
      http.post('/api/submissions/submit/', async ({ request }) => {
        const form = await request.formData();
        sentSubmissionId = form.get('submissionId');
        return HttpResponse.json({ submissionId: 7 });
      }),
    );

    const { submitExhibits } = useSubmissionService();
    const file = new File(['video content'], 'test.mp4', { type: 'video/mp4' });
    await submitExhibits(mockModel, [file], undefined, 7);

    expect(sentSubmissionId).toBe('7');
  });

  it('submitExhibits omits submissionId on the first upload', async () => {
    let hasSubmissionId = true;
    server.use(
      http.post('/api/submissions/submit/', async ({ request }) => {
        const form = await request.formData();
        hasSubmissionId = form.has('submissionId');
        return HttpResponse.json({ submissionId: 7 });
      }),
    );

    const { submitExhibits } = useSubmissionService();
    const file = new File(['video content'], 'test.mp4', { type: 'video/mp4' });
    await submitExhibits(mockModel, [file], undefined, null);

    expect(hasSubmissionId).toBe(false);
  });

  it('retrieveSubmission fetches GET /api/submissions/retrieve', async () => {
    const mockSubmission = { id: 1, status: 'Pending', exhibitCount: 0, tickets: [], files: [] };
    server.use(http.get('/api/submissions/retrieve/', () => HttpResponse.json(mockSubmission)));

    const { retrieveSubmission } = useSubmissionService();
    const result = await retrieveSubmission(1);

    expect(result).toMatchObject({ id: 1 });
  });

  it('retrieveSubmissionListing fetches GET /api/submissions/listing and returns PagedResult', async () => {
    server.use(http.get('/api/submissions/listing/', () => HttpResponse.json(mockPagedResult)));

    const { retrieveSubmissionListing } = useSubmissionService();
    const result = await retrieveSubmissionListing();

    expect(result).toMatchObject({ totalCount: 1, page: 1 });
    expect(result?.items).toHaveLength(1);
  });

  it('retrieveSubmissionListing passes filter params as query string', async () => {
    let capturedUrl = '';
    server.use(
      http.get('/api/submissions/listing/', ({ request }) => {
        capturedUrl = request.url;
        return HttpResponse.json(mockPagedResult);
      }),
    );

    const { retrieveSubmissionListing } = useSubmissionService();
    await retrieveSubmissionListing({ status: 'Pending', page: 2, pageSize: 10 });

    expect(capturedUrl).toContain('status=Pending');
    expect(capturedUrl).toContain('page=2');
    expect(capturedUrl).toContain('pageSize=10');
  });

  it('rejectSubmission sends POST /api/submissions/reject with submissionId', async () => {
    let capturedBody: unknown = null;
    server.use(
      http.post('/api/submissions/reject/', async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json('Submission rejected');
      }),
    );

    const model: SubmissionActionModel = { submissionId: 7 };
    const { rejectSubmission } = useSubmissionService();
    const result = await rejectSubmission(model);

    expect(result).toBe(true);
    expect(capturedBody).toMatchObject({ submissionId: 7 });
  });

  it('getSubmissionsByFileNumber fetches GET /api/submissions/by-file-number', async () => {
    const mockPrior = [{ submissionId: 1, location: 'Test Court', room: 'ROOM1', files: [] }];
    server.use(http.get('/api/submissions/by-file-number', () => HttpResponse.json(mockPrior)));

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
      http.delete(
        `/api/submissions/files/${fileId}`,
        () => new HttpResponse(null, { status: 409 }),
      ),
    );

    const { removeFile } = useSubmissionService();
    const result = await removeFile(fileId);

    expect(result).toBe(false);
  });

  it('markExhibit sends POST /api/files/:fileId/mark and returns updated file', async () => {
    const fileId = '550e8400-e29b-41d4-a716-446655440002';
    const mockFile: SubmissionFile = {
      id: fileId,
      originalFileName: 'exhibit.mp4',
      storedFileName: 'stored.mp4',
      viewUrl: '',
      downloadUrl: '',
      contentType: 'video/mp4',
      fileSize: 1024,
      storageProvider: 'Local',
      status: 'Marked',
      markedValue: 'B',
      descriptions: [],
    };
    let capturedBody: unknown = null;
    server.use(
      http.post(`/api/files/${fileId}/mark`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json(mockFile);
      }),
    );

    const { markExhibit } = useSubmissionService();
    const result = await markExhibit(fileId, { markedValue: 'B' });

    expect(capturedBody).toMatchObject({ markedValue: 'B' });
    expect(result.markedValue).toBe('B');
    expect(result.status).toBe('Marked');
  });

  it('enterExhibit sends POST /api/files/:fileId/enter and returns updated file', async () => {
    const fileId = '550e8400-e29b-41d4-a716-446655440003';
    const mockFile: SubmissionFile = {
      id: fileId,
      originalFileName: 'exhibit.mp4',
      storedFileName: 'stored.mp4',
      viewUrl: '',
      downloadUrl: '',
      contentType: 'video/mp4',
      fileSize: 1024,
      storageProvider: 'Local',
      status: 'Entered',
      enteredValue: '5',
      descriptions: [],
    };
    let capturedBody: unknown = null;
    server.use(
      http.post(`/api/files/${fileId}/enter`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json(mockFile);
      }),
    );

    const { enterExhibit } = useSubmissionService();
    const result = await enterExhibit(fileId, { enteredValue: '5' });

    expect(capturedBody).toMatchObject({ enteredValue: '5' });
    expect(result.enteredValue).toBe('5');
    expect(result.status).toBe('Entered');
  });

  it('addExhibitDescription sends POST /api/files/:fileId/descriptions and returns updated file', async () => {
    const fileId = '550e8400-e29b-41d4-a716-446655440004';
    const mockFile: SubmissionFile = {
      id: fileId,
      originalFileName: 'exhibit.mp4',
      storedFileName: 'stored.mp4',
      viewUrl: '',
      downloadUrl: '',
      contentType: 'video/mp4',
      fileSize: 1024,
      storageProvider: 'Local',
      status: 'Unclassified',
      descriptions: [
        {
          id: 1,
          descriptionText: 'key piece of evidence',
          createdBy: 'officer@test.ca',
          createdAtUTC: '2026-07-07T09:00:00Z',
        },
      ],
    };
    let capturedBody: unknown = null;
    server.use(
      http.post(`/api/files/${fileId}/descriptions`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json(mockFile);
      }),
    );

    const { addExhibitDescription } = useSubmissionService();
    const result = await addExhibitDescription(fileId, 'key piece of evidence');

    expect(capturedBody).toMatchObject({ descriptionText: 'key piece of evidence' });
    expect(result.descriptions[0].descriptionText).toBe('key piece of evidence');
  });

  it('updateEvidenceSource sends PATCH /api/files/:fileId/evidence-source and returns updated file', async () => {
    const fileId = '550e8400-e29b-41d4-a716-446655440009';
    const mockFile: SubmissionFile = {
      id: fileId,
      originalFileName: 'exhibit.mp4',
      storedFileName: 'stored.mp4',
      viewUrl: '',
      downloadUrl: '',
      contentType: 'video/mp4',
      fileSize: 1024,
      storageProvider: 'Local',
      status: 'Unclassified',
      evidenceSourceType: 'DashCam',
      descriptions: [],
    };
    let capturedBody: unknown = null;
    server.use(
      http.patch(`/api/files/${fileId}/evidence-source`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json(mockFile);
      }),
    );

    const { updateEvidenceSource } = useSubmissionService();
    const result = await updateEvidenceSource(fileId, { evidenceSourceType: 'DashCam' });

    expect(capturedBody).toMatchObject({ evidenceSourceType: 'DashCam' });
    expect(result.evidenceSourceType).toBe('DashCam');
  });

  it('searchExhibits builds query params and omits empty ones', async () => {
    let capturedUrl = '';
    server.use(
      http.get('/api/submissions/exhibit-search', ({ request }) => {
        capturedUrl = request.url;
        return HttpResponse.json([]);
      }),
    );

    const { searchExhibits } = useSubmissionService();
    await searchExhibits({
      fileNumberText: 'AH12345',
      accusedName: '',
      appearanceDateFrom: '2026-07-01',
      appearanceDateTo: '',
    });

    expect(capturedUrl).toContain('fileNumberText=AH12345');
    expect(capturedUrl).toContain('appearanceDateFrom=2026-07-01');
    expect(capturedUrl).not.toContain('accusedName');
    expect(capturedUrl).not.toContain('appearanceDateTo');
  });

  it('getExhibitNotes fetches GET /api/files/:fileId/notes', async () => {
    const fileId = '550e8400-e29b-41d4-a716-446655440006';
    server.use(
      http.get(`/api/files/${fileId}/notes`, () =>
        HttpResponse.json([
          { id: 1, noteText: 'registry note', createdBy: 'admin', createdAtUTC: '2026-07-07T09:00:00Z' },
        ]),
      ),
    );

    const { getExhibitNotes } = useSubmissionService();
    const result = await getExhibitNotes(fileId);

    expect(result).toHaveLength(1);
    expect(result[0]).toMatchObject({ id: 1, noteText: 'registry note' });
  });

  it('addExhibitNote posts note text and returns the created note', async () => {
    const fileId = '550e8400-e29b-41d4-a716-446655440007';
    let capturedBody: unknown = null;
    server.use(
      http.post(`/api/files/${fileId}/notes`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({
          id: 2,
          noteText: 'hello',
          createdBy: 'admin',
          createdAtUTC: '2026-07-07T10:00:00Z',
        });
      }),
    );

    const { addExhibitNote } = useSubmissionService();
    const result = await addExhibitNote(fileId, 'hello');

    expect(capturedBody).toMatchObject({ noteText: 'hello' });
    expect(result.id).toBe(2);
    expect(result.noteText).toBe('hello');
  });

  it('getFileHistory fetches GET /api/files/:fileId/history and returns entries', async () => {
    const fileId = '550e8400-e29b-41d4-a716-446655440005';
    const mockHistory = [
      {
        fieldName: 'MarkedValue',
        oldValue: null,
        newValue: 'B',
        changedBy: 'officer@test.ca',
        changedAtUTC: '2026-01-01T09:00:00Z',
      },
    ];
    let capturedUrl = '';
    server.use(
      http.get(`/api/files/${fileId}/history`, ({ request }) => {
        capturedUrl = request.url;
        return HttpResponse.json(mockHistory);
      }),
    );

    const { getFileHistory } = useSubmissionService();
    const result = await getFileHistory(fileId);

    expect(capturedUrl).toContain(`/api/files/${fileId}/history`);
    expect(result).toHaveLength(1);
    expect(result[0]).toMatchObject({ fieldName: 'MarkedValue', newValue: 'B' });
  });
});
