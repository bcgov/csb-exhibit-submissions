import { defineStore } from 'pinia';
import type { CourtFileList } from '@/models/CourtFileList';

export const useCourtFileSelectionStore = defineStore('courtSelection', {
  state: () => ({
    selectedFiles: [] as CourtFileList[],
  }),
  getters: {
    // Convenience accessor — first selected file, used by components that haven't migrated yet.
    selectedFile: (state): CourtFileList | null => state.selectedFiles[0] ?? null,
  },
  actions: {
    setSelectedFiles(files: CourtFileList[]) {
      this.selectedFiles = files;
    },
    removeFile(appearanceId: string) {
      if (this.selectedFiles.length <= 1) return;
      this.selectedFiles = this.selectedFiles.filter(f => f.appearanceId !== appearanceId);
    },
    clear() {
      this.selectedFiles = [];
    },
  },
});
