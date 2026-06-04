import { createPinia, setActivePinia } from 'pinia'
import { useCourtFileSelectionStore } from '@/stores/useCourtFileSelectionStore'
import type { CourtFileList } from '@/models/CourtFileList'

const mockFile: CourtFileList = {
  appearanceID: 'APP001',
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
}

beforeEach(() => {
  setActivePinia(createPinia())
})

describe('courtFileSelectionStore', () => {
  it('initial state is empty', () => {
    const store = useCourtFileSelectionStore()
    expect(store.selectedFile).toBeNull()
  })

  it('setSelectedFile populates selection', () => {
    const store = useCourtFileSelectionStore()
    store.setSelectedFile(mockFile)
    expect(store.selectedFile).not.toBeNull()
    expect(store.selectedFile?.appearanceID).toBe('APP001')
  })

  it('setSelectedFile replaces previous selection', () => {
    const store = useCourtFileSelectionStore()
    store.setSelectedFile(mockFile)
    const newFile = { ...mockFile, appearanceID: 'APP002' }
    store.setSelectedFile(newFile)
    expect(store.selectedFile?.appearanceID).toBe('APP002')
  })

  it('clear empties all selections', () => {
    const store = useCourtFileSelectionStore()
    store.setSelectedFile(mockFile)
    store.clear()
    expect(store.selectedFile).toBeNull()
  })
})
