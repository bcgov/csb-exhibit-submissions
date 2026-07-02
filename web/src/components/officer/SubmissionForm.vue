<script setup lang="ts">
import { formatDateyyyymmdd } from '@/helpers/formatters';
import type {
  ExhibitSubmissionModel,
  SubmissionTicketModel,
} from '@/models/ExhibitSubmissionModel';
import type { PriorSubmissionModel } from '@/models/PriorSubmissionModel';
import type { SubmissionFile } from '@/models/SubmissionReviewModel';
import useSubmissionService from '@/services/SubmissionService';
import { useCourtFileSelectionStore } from '@/stores/useCourtFileSelectionStore';
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import ExhibitList from '../shared/ExhibitList.vue';
import FileDropZone from '../shared/FileDropZone.vue';
import FileViewer from '../shared/FileViewer.vue';

const router = useRouter();
const {
  submitExhibits,
  getSubmissionsByFileNumber,
  markExhibit,
  enterExhibit,
  updateExhibitDescription,
} = useSubmissionService();
const selectionStore = useCourtFileSelectionStore();

const uploading = ref(false);
const errorMessage = ref('');
const successMessage = ref('');
const uploadProgress = ref<number>(0);
const officerNumber = ref('');
const dropZoneRef = ref<InstanceType<typeof FileDropZone> | null>(null);

// Active submission for this page session. Set after the first successful upload so that
// subsequent uploads append to the same submission. Reset on reload/search/Back (component state).
const currentSubmissionId = ref<number | null>(null);

const priorExhibits = ref<Map<string, PriorSubmissionModel[]>>(new Map());
const priorExhibitsError = ref(false);

// Preview/view modal (officer view-only, no download)
const previewFile = ref<SubmissionFile | null>(null);

// Tickets managed locally so the officer can remove some before submitting.
const tickets = ref<SubmissionTicketModel[]>([]);

const sharedDate = computed(() => {
  const dt = selectionStore.selectedFiles[0]?.appearanceDateTime ?? '';
  return formatDateyyyymmdd(dt);
});
const sharedLocation = computed(() => selectionStore.selectedFiles[0]?.locationNameText ?? '');
const sharedRoom = computed(() => {
  const code = selectionStore.selectedFiles[0]?.roomCode ?? '';
  return code ? `Room ${code}` : '';
});

const files = ref<File[]>([]);

const handleFilesChanged = (newFiles: File[]) => {
  files.value = newFiles;
};

const updateProgress = (percent: number) => {
  uploadProgress.value = percent;
};

const removeTicket = (appearanceId: string) => {
  // Tickets are locked once a submission is active for this session.
  if (currentSubmissionId.value !== null) return;
  if (tickets.value.length <= 1) return;
  tickets.value = tickets.value.filter((t) => t.appearanceId !== appearanceId);
};

// Return a deduplicated list of file numbers across the current ticket set.
const uniqueFileNumbers = computed(() => [...new Set(tickets.value.map((t) => t.fileNumberText))]);

// Flat list of prior files across all queried file numbers, deduplicated by file ID.
const flatPriorFiles = computed(() => {
  const activeFileNumbers = new Set(uniqueFileNumbers.value);
  const submissionFileNumbers = new Map<number, Set<string>>();
  const fileMap = new Map<
    string,
    { file: SubmissionFile; submissionDate?: string; submissionId: number }
  >();

  for (const [fn, submissions] of priorExhibits.value) {
    if (!activeFileNumbers.has(fn)) continue;
    for (const sub of submissions) {
      if (!submissionFileNumbers.has(sub.submissionId)) {
        submissionFileNumbers.set(sub.submissionId, new Set());
      }
      submissionFileNumbers.get(sub.submissionId)!.add(fn);

      for (const f of sub.files) {
        if (f.status === 'Removed') continue;
        if (!fileMap.has(f.id)) {
          fileMap.set(f.id, {
            file: f,
            submissionDate: sub.submissionDate,
            submissionId: sub.submissionId,
          });
        }
      }
    }
  }

  return [...fileMap.values()].map(({ file, submissionDate, submissionId }) => ({
    file,
    submissionDate,
    fileNumbers: [...(submissionFileNumbers.get(submissionId) ?? [])],
  }));
});

const goBack = () => {
  selectionStore.clear();
  router.push({ name: 'OfficerCourtList' });
};

const loadPriorExhibits = async () => {
  priorExhibitsError.value = false;
  const results = new Map<string, PriorSubmissionModel[]>();
  try {
    await Promise.all(
      uniqueFileNumbers.value.map(async (fn) => {
        const data = await getSubmissionsByFileNumber(fn);
        results.set(fn, data);
      }),
    );
    priorExhibits.value = results;
  } catch {
    priorExhibitsError.value = true;
  }
};

const updateFileInStore = (updated: SubmissionFile) => {
  for (const submissions of priorExhibits.value.values()) {
    for (const sub of submissions) {
      const idx = sub.files.findIndex((f) => f.id === updated.id);
      if (idx !== -1) {
        sub.files[idx] = { ...sub.files[idx], ...updated };
        return;
      }
    }
  }
};

const openPreview = (file: SubmissionFile) => {
  previewFile.value = file;
};
const closePreview = () => {
  previewFile.value = null;
};

onMounted(async () => {
  if (selectionStore.selectedFiles.length === 0) {
    router.push({ name: 'OfficerCourtList' });
    return;
  }

  tickets.value = selectionStore.selectedFiles.map((f) => ({
    appearanceId: f.appearanceId,
    appearanceDateTime: f.appearanceDateTime,
    appearanceSequenceNumber: f.appearanceSequenceNumber,
    appearanceReasonCode: f.appearanceReasonCode,
    courtListType: f.courtListType,
    fileNumberText: f.fileNumberText,
    accusedName: f.accusedName,
    accusedDOB: f.accusedDOB,
  }));

  await loadPriorExhibits();
});

const submitForm = async () => {
  uploading.value = true;
  errorMessage.value = '';

  const submission: ExhibitSubmissionModel = {
    tickets: tickets.value,
    shortDate: sharedDate.value,
    appearanceDateTime: selectionStore.selectedFiles[0]?.appearanceDateTime ?? '',
    locationId: selectionStore.selectedFiles[0]?.locationId ?? '',
    locationNameText: selectionStore.selectedFiles[0]?.locationNameText ?? '',
    roomCode: selectionStore.selectedFiles[0]?.roomCode ?? '',
    roomText: selectionStore.selectedFiles[0]?.roomText ?? '',
    officerNumber: officerNumber.value,
  };

  let submissionId: number | null = null;
  try {
    submissionId = await submitExhibits(
      submission,
      files.value,
      updateProgress,
      currentSubmissionId.value,
    );
  } catch (error) {
    console.error('Upload failed', error);
    errorMessage.value = 'Failed to upload exhibit. Please try again.';
  } finally {
    uploading.value = false;
    if (submissionId !== null) {
      // Retain the id so further uploads on this page attach to the same submission.
      currentSubmissionId.value = submissionId;
      uploadProgress.value = 0;
      files.value = [];
      dropZoneRef.value?.reset();
      successMessage.value = 'Exhibit uploaded successfully.';
      await loadPriorExhibits();
    } else if (!errorMessage.value) {
      errorMessage.value = 'Upload failed. Please ensure at least one file is selected.';
    }
  }
};
</script>

<template>
  <div class="exhibit-page">
    <h1>Exhibit Upload</h1>

    <form @submit.prevent="submitForm">
      <!-- Shared read-only fields -->
      <div class="shared-fields">
        <div class="form-field">
          <label>Date</label>
          <input type="date" :value="sharedDate" disabled />
        </div>
        <div class="form-field">
          <label>Location</label>
          <input type="text" :value="sharedLocation" disabled />
        </div>
        <div class="form-field">
          <label>Room</label>
          <input type="text" :value="sharedRoom" disabled />
        </div>
      </div>

      <!-- Officer number -->
      <div class="officer-field">
        <label>Officer Number</label>
        <input type="text" v-model="officerNumber" />
      </div>

      <!-- Ticket list panel -->
      <div class="ticket-panel">
        <div class="ticket-panel-header">
          <span>Tickets ({{ tickets.length }})</span>
        </div>
        <div v-for="ticket in tickets" :key="ticket.appearanceId" class="ticket-row">
          <div class="ticket-info">
            <span class="ticket-file-num">{{ ticket.fileNumberText }}</span>
            <span class="ticket-detail"> — {{ ticket.accusedName }}</span>
            <span v-if="ticket.appearanceDateTime" class="ticket-detail">
              &nbsp;@ {{ ticket.appearanceDateTime.split('T')[1]?.slice(0, 5) }}
            </span>
          </div>
          <button
            v-if="tickets.length > 1 && currentSubmissionId === null"
            type="button"
            class="btn btn--sm btn--danger-outline remove-btn"
            @click="removeTicket(ticket.appearanceId)"
          >
            Remove
          </button>
        </div>
      </div>

      <!-- Prior exhibits panel (editable) -->
      <div v-if="uniqueFileNumbers.length > 0" class="prior-exhibits-section">
        <h4>Prior Exhibits</h4>

        <p v-if="priorExhibitsError" class="prior-error">
          Could not load prior exhibit history. You can still proceed with the upload.
        </p>

        <ExhibitList
          v-else-if="flatPriorFiles.length > 0"
          :entries="flatPriorFiles"
          :mark-fn="(id: string, v: string) => markExhibit(id, { markedValue: v })"
          :enter-fn="(id: string, v: string) => enterExhibit(id, { enteredValue: v })"
          :description-fn="
            (id: string, d: string) => updateExhibitDescription(id, { description: d })
          "
          @file-updated="updateFileInStore"
          @preview-file="openPreview"
        />

        <p v-else class="prior-empty">No previous exhibits for the selected tickets.</p>
      </div>

      <!-- Dropzone -->
      <FileDropZone ref="dropZoneRef" @filesChanged="handleFilesChanged" />

      <div class="upload-progress">
        <div class="progress" style="height: 20px">
          <div
            class="progress-bar progress-bar-striped progress-bar-animated bg-primary"
            role="progressbar"
            :style="{ width: uploadProgress + '%' }"
            :aria-valuenow="uploadProgress"
            aria-valuemin="0"
            aria-valuemax="100"
          ></div>
        </div>
      </div>

      <span v-if="successMessage" class="success-text">{{ successMessage }}</span>
      <span v-if="errorMessage" class="error-text">{{ errorMessage }}</span>

      <div class="actions">
        <button type="button" class="btn btn--secondary back-btn" @click="goBack">Back</button>
        <button type="submit" class="btn btn--primary submit-btn" :disabled="uploading">
          Attach Exhibit
        </button>
      </div>
    </form>

    <!-- Officer view-only preview modal (no download offered) -->
    <div v-if="previewFile" class="preview-overlay" @click.self="closePreview">
      <div class="preview-dialog">
        <button
          type="button"
          class="btn btn--icon btn--tertiary close-btn"
          aria-label="Close preview"
          @click="closePreview"
        >
          ✖
        </button>
        <FileViewer
          :fileUrl="`/api/files/${previewFile.id}/view`"
          :mimeType="previewFile.contentType"
          :hideDownload="true"
        />
      </div>
    </div>
  </div>
</template>
