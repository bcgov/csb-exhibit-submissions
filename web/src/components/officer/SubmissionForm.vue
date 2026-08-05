<script setup lang="ts">
import { formatDateyyyymmdd } from '@/helpers/formatters';
import type {
  ExhibitSubmissionModel,
  SubmissionTicketModel,
} from '@/models/ExhibitSubmissionModel';
import type { ExhibitSearchResultModel } from '@/models/ExhibitSearchResultModel';
import type { PriorSubmissionModel } from '@/models/PriorSubmissionModel';
import type { SubmissionFile } from '@/models/SubmissionReviewModel';
import useSubmissionService from '@/services/SubmissionService';
import { useCourtFileSelectionStore } from '@/stores/useCourtFileSelectionStore';
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import ExhibitDetailModal from '../shared/ExhibitDetailModal.vue';
import ExhibitList from '../shared/ExhibitList.vue';
import FileDropZone from '../shared/FileDropZone.vue';
import FileViewer from '../shared/FileViewer.vue';

const router = useRouter();
const {
  submitExhibits,
  getSubmissionsByFileNumber,
  markExhibit,
  enterExhibit,
  addExhibitDescription,
  updateEvidenceSource,
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

// Exhibit detail modal — the full context of the exhibit whose name was clicked.
const detailResult = ref<ExhibitSearchResultModel | null>(null);

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

// Every prior exhibit across the queried file numbers, deduplicated by file ID and
// keyed by it. Carries the full submission context each exhibit came from so the
// detail modal can be opened straight from the list (CES-42).
const exhibitContexts = computed(() => {
  const activeFileNumbers = new Set(uniqueFileNumbers.value);
  const submissionFileNumbers = new Map<number, Set<string>>();
  const contexts = new Map<string, ExhibitSearchResultModel>();

  for (const [fn, submissions] of priorExhibits.value) {
    if (!activeFileNumbers.has(fn)) continue;
    for (const sub of submissions) {
      if (!submissionFileNumbers.has(sub.submissionId)) {
        submissionFileNumbers.set(sub.submissionId, new Set());
      }
      submissionFileNumbers.get(sub.submissionId)!.add(fn);

      for (const f of sub.files) {
        if (f.status === 'Removed') continue;
        if (contexts.has(f.id)) continue;
        contexts.set(f.id, {
          file: f,
          submissionId: sub.submissionId,
          submissionDate: sub.submissionDate,
          appearanceDateTime: sub.appearanceDateTime,
          location: sub.location,
          room: sub.room,
          // Filled in below, once every file number for this submission is known.
          fileNumbers: [],
          accusedName: tickets.value.find((t) => t.fileNumberText === fn)?.accusedName,
        });
      }
    }
  }

  for (const context of contexts.values()) {
    context.fileNumbers = [...(submissionFileNumbers.get(context.submissionId) ?? [])];
  }

  return contexts;
});

// The PriorFileEntry shape ExhibitList consumes.
const flatPriorFiles = computed(() =>
  [...exhibitContexts.value.values()].map((c) => ({
    file: c.file,
    submissionDate: c.submissionDate,
    fileNumbers: c.fileNumbers,
  })),
);

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

// Exhibit detail popup (CES-42). Officers see the same modal admins do, minus the
// registry-only Notes section.
const openDetails = (file: SubmissionFile) => {
  detailResult.value = exhibitContexts.value.get(file.id) ?? null;
};
const closeDetails = () => {
  detailResult.value = null;
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

      <!-- Attached exhibits panel (editable) -->
      <div v-if="uniqueFileNumbers.length > 0" class="prior-exhibits-section">
        <h2>Attached Exhibits</h2>

        <p v-if="priorExhibitsError" class="prior-error">
          Could not load prior exhibit history. You can still proceed with the upload.
        </p>

        <ExhibitList
          v-else-if="flatPriorFiles.length > 0"
          :entries="flatPriorFiles"
          :mark-fn="(id: string, v: string) => markExhibit(id, { markedValue: v })"
          :enter-fn="(id: string, v: string) => enterExhibit(id, { enteredValue: v })"
          :add-description-fn="addExhibitDescription"
          :initial-expanded="true"
          :linkable-title="true"
          :sort-by-classification="true"
          :evidence-source-fn="
            (id: string, v: string) => updateEvidenceSource(id, { evidenceSourceType: v })
          "
          @file-updated="updateFileInStore"
          @preview-file="openPreview"
          @title-click="openDetails"
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

    <!-- Exhibit details (description history, change history). No registry notes for officers. -->
    <ExhibitDetailModal
      v-if="detailResult"
      :result="detailResult"
      :add-description-fn="addExhibitDescription"
      @file-updated="updateFileInStore"
      @close="closeDetails"
    />
  </div>
</template>
