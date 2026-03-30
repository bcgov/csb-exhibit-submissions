// stores/useCourtSelectionStore.ts
import { defineStore } from 'pinia'
import type { CourtFileList } from '@/models/CourtFileList'

export const useCourtFileSelectionStore = defineStore('courtSelection', {
  state: () => ({
    selectedFile: null as CourtFileList | null
  }),
  actions: {
    setSelectedFile(file: CourtFileList) {
      this.selectedFile = file
    },
    clear() {
      this.selectedFile = null
    }
  }
})