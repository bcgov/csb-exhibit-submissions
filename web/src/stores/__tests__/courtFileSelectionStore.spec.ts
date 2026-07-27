import { createPinia, setActivePinia } from 'pinia';
import { useCourtFileSelectionStore } from '@/stores/useCourtFileSelectionStore';
import type { CourtFileList } from '@/models/CourtFileList';

const mockFile: CourtFileList = {
  appearanceId: 'APP001',
  appearanceDateTime: '2026-01-01T09:00:00',
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
  appearanceDetails: [],
};

const mockFile2: CourtFileList = { ...mockFile, appearanceId: 'APP002', fileNumberText: 'FILE002' };

beforeEach(() => {
  setActivePinia(createPinia());
});

describe('courtFileSelectionStore', () => {
  it('initial state is empty', () => {
    const store = useCourtFileSelectionStore();
    expect(store.selectedFiles).toHaveLength(0);
    expect(store.selectedFile).toBeNull();
  });

  it('setSelectedFiles populates selection', () => {
    const store = useCourtFileSelectionStore();
    store.setSelectedFiles([mockFile]);
    expect(store.selectedFiles).toHaveLength(1);
    expect(store.selectedFiles[0]?.appearanceId).toBe('APP001');
  });

  it('setSelectedFiles replaces previous selection entirely', () => {
    const store = useCourtFileSelectionStore();
    store.setSelectedFiles([mockFile]);
    store.setSelectedFiles([mockFile2]);
    expect(store.selectedFiles).toHaveLength(1);
    expect(store.selectedFiles[0]?.appearanceId).toBe('APP002');
  });

  it('setSelectedFiles supports multiple tickets', () => {
    const store = useCourtFileSelectionStore();
    store.setSelectedFiles([mockFile, mockFile2]);
    expect(store.selectedFiles).toHaveLength(2);
  });

  it('selectedFile getter returns first ticket', () => {
    const store = useCourtFileSelectionStore();
    store.setSelectedFiles([mockFile, mockFile2]);
    expect(store.selectedFile?.appearanceId).toBe('APP001');
  });

  it('removeFile removes a ticket by appearanceId', () => {
    const store = useCourtFileSelectionStore();
    store.setSelectedFiles([mockFile, mockFile2]);
    store.removeFile('APP001');
    expect(store.selectedFiles).toHaveLength(1);
    expect(store.selectedFiles[0]?.appearanceId).toBe('APP002');
  });

  it('removeFile does not remove the last ticket', () => {
    const store = useCourtFileSelectionStore();
    store.setSelectedFiles([mockFile]);
    store.removeFile('APP001');
    expect(store.selectedFiles).toHaveLength(1);
  });

  it('clear empties all selections', () => {
    const store = useCourtFileSelectionStore();
    store.setSelectedFiles([mockFile, mockFile2]);
    store.clear();
    expect(store.selectedFiles).toHaveLength(0);
    expect(store.selectedFile).toBeNull();
  });
});
