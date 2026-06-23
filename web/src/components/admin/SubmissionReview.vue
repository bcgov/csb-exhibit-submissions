<script setup lang="ts">
import {
  DESCRIPTION_MAX_LENGTH,
  ENTERED_MAX,
  ENTERED_MIN,
  MARKED_MAX,
  MARKED_MIN,
  VIEWABLE_CONTENT_TYPE_PREFIXES,
} from '@/constants/classification';
import { convertUtcToLocal, formatDateTime, formatFileSize, shortenString, splitDateTimeForDisplay } from '@/helpers/formatters';
import type { SubmissionActionModel, SubmissionFile, SubmissionReviewModel } from '@/models/SubmissionReviewModel';
import useSubmissionService from '@/services/SubmissionService';
import { computed, onMounted, reactive, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import AppModal from '../shared/AppModal.vue';
import FileViewer from '../shared/FileViewer.vue';

const route = useRoute();
const router = useRouter();

const submissionId = Number(route.params.id);

const {
  retrieveSubmission,
  acceptSubmission,
  rejectSubmission,
  removeFile,
  markExhibit,
  enterExhibit,
  updateExhibitDescription,
} = useSubmissionService();

const submission = ref<SubmissionReviewModel | undefined>(undefined);
const acceptError = ref<string | null>(null);
const showRejectModal = ref(false);
const removeError = ref<string | null>(null);
const previewFile = ref<SubmissionFile | null>(null);

// Save indicator state: null = idle, true = saved, false/string = error
const saveIndicators = reactive<Record<string, boolean | string | null>>({});
// Local description values for two-way binding before blur
const localDescriptions = reactive<Record<string, string>>({});

const markedLetters = Array.from({ length: 26 }, (_, i) => String.fromCharCode(MARKED_MIN.charCodeAt(0) + i));
const enteredNumbers = Array.from({ length: ENTERED_MAX - ENTERED_MIN + 1 }, (_, i) => String(ENTERED_MIN + i));

const getFileUrl = (fileId: string, action: 'view' | 'download') => `/api/files/${fileId}/${action}`;

const isTerminal = computed(() => submission.value?.status === 'Accepted' || submission.value?.status === 'Rejected');

const acceptReadiness = computed((): { ready: boolean; blockingNames: string[] } => {
  if (!submission.value) return { ready: false, blockingNames: [] };
  const blocking = submission.value.files
    .filter(f => !f.deletedAt && f.enteredValue == null)
    .map(f => f.originalFileName);
  return { ready: blocking.length === 0, blockingNames: blocking };
});

const isViewable = (contentType: string) =>
  VIEWABLE_CONTENT_TYPE_PREFIXES.some(p => contentType.startsWith(p));

onMounted(async () => {
  const data = await retrieveSubmission(submissionId);
  if (!data) return;

  submission.value = {
    ...data,
    files: data.files.map((f: SubmissionFile) => ({
      ...f,
      viewUrl: getFileUrl(f.id, 'view'),
      downloadUrl: getFileUrl(f.id, 'download'),
    })),
  };

  // Seed local descriptions
  submission.value.files.forEach(f => {
    localDescriptions[f.id] = f.description ?? '';
  });
});

const openPreview = (file: SubmissionFile) => {
  previewFile.value = file;
};
const closePreview = () => {
  previewFile.value = null;
};

const downloadFile = async (file: SubmissionFile) => {
  try {
    const response = await fetch(file.downloadUrl);
    if (!response.ok) return;
    const blob = await response.blob();
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = file.originalFileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
    window.URL.revokeObjectURL(url);
  } catch (err) {
    console.error('Download error:', err);
  }
};

const setSaveIndicator = (fileId: string, value: boolean | string | null) => {
  saveIndicators[fileId] = value;
  if (value !== null) {
    setTimeout(() => { saveIndicators[fileId] = null; }, 5000);
  }
};

const updateFileInSubmission = (updated: SubmissionFile) => {
  if (!submission.value) return;
  submission.value = {
    ...submission.value,
    files: submission.value.files.map(f =>
      f.id === updated.id ? { ...f, ...updated, viewUrl: f.viewUrl, downloadUrl: f.downloadUrl } : f,
    ),
  };
};

const onMarkChange = async (file: SubmissionFile, value: string) => {
  try {
    const updated = await markExhibit(file.id, { markedValue: value });
    updateFileInSubmission(updated);
    setSaveIndicator(file.id, true);
  } catch {
    setSaveIndicator(file.id, 'Failed to save Marked value');
  }
};

const onEnterChange = async (file: SubmissionFile, value: string) => {
  if (!value) return;
  try {
    const updated = await enterExhibit(file.id, { enteredValue: value });
    updateFileInSubmission(updated);
    setSaveIndicator(file.id, true);
  } catch {
    setSaveIndicator(file.id, 'Failed to save Entered value');
  }
};

const onDescriptionBlur = async (file: SubmissionFile) => {
  const desc = localDescriptions[file.id] ?? '';
  if (desc === (file.description ?? '')) return;
  try {
    const updated = await updateExhibitDescription(file.id, { description: desc });
    updateFileInSubmission(updated);
    setSaveIndicator(file.id, true);
  } catch {
    setSaveIndicator(file.id, 'Failed to save description');
  }
};

const doAcceptSubmission = async () => {
  if (!acceptReadiness.value.ready) {
    acceptError.value = `${acceptReadiness.value.blockingNames.length} exhibit(s) not yet Entered or Removed.`;
    return;
  }
  acceptError.value = null;
  const payload: SubmissionActionModel = { submissionId };
  const ok = await acceptSubmission(payload);
  if (ok) {
    router.push('/admin/list');
  } else {
    acceptError.value = 'Accept failed. Ensure all exhibits are Entered or Removed.';
  }
};

const doRejectSubmission = async () => {
  const payload: SubmissionActionModel = { submissionId };
  await rejectSubmission(payload);
  router.push('/admin/list');
};

const removeExhibit = async (file: SubmissionFile) => {
  removeError.value = null;
  const success = await removeFile(file.id);
  if (success && submission.value) {
    // Mark as removed in place (keep in list, greyed out)
    submission.value = {
      ...submission.value,
      files: submission.value.files.map(f =>
        f.id === file.id ? { ...f, status: 'Removed', deletedAt: new Date().toISOString() } : f,
      ),
    };
  } else if (!success) {
    removeError.value = 'Could not remove exhibit.';
  }
};

const fileIcon = (type: string) => {
  if (type.startsWith('image')) return '🖼';
  if (type.startsWith('video')) return '🎬';
  if (type.includes('pdf')) return '📄';
  return '📁';
};

const formatClassificationDate = (iso?: string | null): string => {
  if (!iso) return '—';
  return formatDateTime(convertUtcToLocal(iso), true);
};
</script>

<template>
  <div class="review-page">
    <h1>Submission Review</h1>

    <div v-if="submission">
      <div class="details-grid">
        <div><strong>Court Date:</strong> {{ splitDateTimeForDisplay(submission.courtDateTime).date }}</div>
        <div><strong>Court Time:</strong> {{ splitDateTimeForDisplay(submission.courtDateTime).time }}</div>
        <div><strong>Location:</strong> {{ submission.location }}</div>
        <div><strong>Room:</strong> {{ submission.room }}</div>
        <div><strong>Submission Date:</strong> {{ submission.submissionDate ?
          formatDateTime(convertUtcToLocal(submission.submissionDate), true) : '' }}</div>
        <div>
          <strong>Status:</strong>
          <span :class="`status-chip status-${submission.status.toLowerCase()}`">{{ submission.status }}</span>
        </div>
      </div>

      <!-- Tickets section -->
      <h3>Tickets ({{ submission.tickets?.length ?? 0 }})</h3>
      <table class="ticket-table">
        <thead>
          <tr>
            <th>File #</th>
            <th>Accused Name</th>
            <th>Appearance Time</th>
            <th>Appearance Reason</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="ticket in submission.tickets" :key="ticket.appearanceId">
            <td class="text-monospace">{{ ticket.fileNumberText }}</td>
            <td>{{ ticket.accusedName }}</td>
            <td>{{ ticket.appearanceDateTime?.split(/[T ]/)[1]?.slice(0, 5) ?? '' }}</td>
            <td>{{ ticket.appearanceReasonCode }}</td>
          </tr>
        </tbody>
      </table>

      <h3>Submitted Evidence</h3>

      <div class="file-list">
        <div
          class="file-row"
          v-for="file in submission.files"
          :key="file.id"
          :class="{ 'file-row-removed': file.status === 'Removed' }"
        >
          <div class="file-left">
            <span class="icon">{{ fileIcon(file.contentType) }}</span>
            <span class="name">{{ shortenString(file.originalFileName) }}</span>
          </div>
          <div class="file-size">{{ formatFileSize(file.fileSize) }}</div>

          <!-- Classification display -->
          <div class="classification-info">
            <span class="cl-chip" :class="`cl-${(file.status ?? 'Unclassified').toLowerCase()}`">
              {{ file.status ?? 'Unclassified' }}
            </span>
            <span v-if="file.markedValue" class="cl-field">M: {{ file.markedValue }} <small>({{ formatClassificationDate(file.markedAt) }})</small></span>
            <span v-if="file.enteredValue" class="cl-field">E: {{ file.enteredValue }} <small>({{ formatClassificationDate(file.enteredAt) }})</small></span>
            <span v-if="file.description" class="cl-field cl-desc" :title="file.description">{{ file.description }}</span>
          </div>

          <!-- Admin classification controls — only for Pending submissions, non-Removed files -->
          <template v-if="!isTerminal && file.status !== 'Removed'">
            <div class="classification-controls">
              <!-- Marked -->
              <div class="classification-group">
                <label>Marked</label>
                <select :value="file.markedValue ?? ''"
                  @change="onMarkChange(file, ($event.target as HTMLSelectElement).value)">
                  <option value="">—</option>
                  <option v-for="letter in markedLetters" :key="letter" :value="letter">{{ letter }}</option>
                </select>
              </div>

              <!-- Entered -->
              <div class="classification-group">
                <label>Entered</label>
                <select :value="file.enteredValue ?? ''"
                  @change="onEnterChange(file, ($event.target as HTMLSelectElement).value)">
                  <option value="">—</option>
                  <option v-for="num in enteredNumbers" :key="num" :value="num">{{ num }}</option>
                </select>
              </div>

              <!-- Description -->
              <div class="description-group">
                <label>Description</label>
                <input
                  type="text"
                  :maxlength="DESCRIPTION_MAX_LENGTH"
                  v-model="localDescriptions[file.id]"
                  @blur="onDescriptionBlur(file)"
                  placeholder="Optional description…"
                />
                <span class="desc-counter"
                  :class="{ over: (localDescriptions[file.id]?.length ?? 0) > DESCRIPTION_MAX_LENGTH }">
                  {{ DESCRIPTION_MAX_LENGTH - (localDescriptions[file.id]?.length ?? 0) }} remaining
                </span>
              </div>

              <span v-if="saveIndicators[file.id] === true" class="save-indicator save-ok">✓</span>
              <span v-else-if="saveIndicators[file.id]" class="save-indicator save-error"
                :title="saveIndicators[file.id] as string">✕</span>
            </div>

            <div class="file-actions">
              <button v-if="isViewable(file.contentType)" @click="openPreview(file)">View</button>
              <button @click="downloadFile(file)">Download</button>
              <button class="remove-file-btn" @click="removeExhibit(file)">Remove</button>
            </div>
          </template>

          <!-- View/Download for terminal states (non-Removed only) -->
          <template v-else-if="isTerminal && file.status !== 'Removed'">
            <div class="file-actions">
              <button v-if="isViewable(file.contentType)" @click="openPreview(file)">View</button>
              <button @click="downloadFile(file)">Download</button>
            </div>
          </template>
        </div>
      </div>

      <p v-if="removeError" class="remove-error">{{ removeError }}</p>

      <!-- Actions: only shown for Pending -->
      <template v-if="!isTerminal">
        <div class="actions-main">
          <button
            class="accept"
            :disabled="!acceptReadiness.ready"
            :title="acceptReadiness.ready ? 'Accept this submission' : `${acceptReadiness.blockingNames.length} exhibit(s) not yet Entered or Removed`"
            @click="doAcceptSubmission"
          >Accept</button>
          <button class="remove" @click="showRejectModal = true">Reject Submission</button>
        </div>
        <p v-if="acceptError" class="accept-error">{{ acceptError }}</p>
      </template>
    </div>

    <!-- Reject confirmation modal -->
    <AppModal
      v-if="showRejectModal"
      title="Reject Submission"
      confirm-label="Reject Submission"
      :confirm-danger="true"
      @confirm="showRejectModal = false; doRejectSubmission()"
      @cancel="showRejectModal = false"
    >
      Rejecting this submission permanently deletes <strong>all</strong> associated files. This cannot be undone and the files are unretrievable.
    </AppModal>

    <div v-if="previewFile" class="preview-modal">
      <div class="modal-content">
        <button class="close" @click="closePreview">✖</button>
        <FileViewer :fileUrl="previewFile.viewUrl" :download-url="previewFile.downloadUrl"
          :mimeType="previewFile.contentType" />
      </div>
    </div>
  </div>
</template>
