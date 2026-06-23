<script setup lang="ts">
import { convertUtcToLocal, formatDateTime, formatFileSize, shortenString, splitDateTimeForDisplay } from '@/helpers/formatters';
import type { SubmissionAcceptanceModel } from '@/models/SubmissionAcceptanceModel';
import type { SubmissionFile, SubmissionReviewModel } from '@/models/SubmissionReviewModel';
import useSubmissionService from '@/services/SubmissionService';
import { onMounted, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import AppModal from '../shared/AppModal.vue';
import FileViewer from '../shared/FileViewer.vue';

const route = useRoute();
const router = useRouter();

const submissionId = Number(route.params.id);

const previewFile = ref<SubmissionFile | null>(null);

const { retrieveSubmission, acceptSubmissionFiles, rejectAndCloseSubmission, removeFile } = useSubmissionService();

const submission = ref<SubmissionReviewModel | undefined>(undefined);
const selectedFiles = ref<string[]>([]);
const acceptError = ref<string | null>(null);
const showRejectModal = ref(false);
const removeError = ref<string | null>(null);

const getFileUrl = (fileId: string, action: 'view' | 'download') => `/api/files/${fileId}/${action}`;

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

  selectedFiles.value = submission.value.files.map(f => f.id);
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

    if (response.status === 404) {
      console.warn('File not found');
      return;
    }

    if (!response.ok) {
      console.error(`File not found (${response.status})`);
      return;
    }

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

const acceptSubmission = async () => {
  if (selectedFiles.value.length === 0) {
    acceptError.value = 'Please select at least one file.';
    return;
  }
  acceptError.value = null;

  const payload: SubmissionAcceptanceModel = {
    fileId: submissionId,
    acceptedFiles: selectedFiles.value,
  };

  const returnValue = await acceptSubmissionFiles(payload);
  console.log(returnValue, 'return value');
  router.push('/admin/list');
};

const removeSubmission = async () => {
  const payload: SubmissionAcceptanceModel = {
    fileId: submissionId,
    acceptedFiles: selectedFiles.value,
  };
  await rejectAndCloseSubmission(payload);
  router.push('/admin/list');
};

const removeExhibit = async (file: SubmissionFile) => {
  removeError.value = null;
  const success = await removeFile(file.id);
  if (success && submission.value) {
    submission.value = {
      ...submission.value,
      files: submission.value.files.filter(f => f.id !== file.id),
    };
    selectedFiles.value = selectedFiles.value.filter(id => id !== file.id);
  } else if (!success) {
    removeError.value = 'Could not remove exhibit. It may already be Entered.';
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
        <div class="file-row" v-for="file in submission.files" :key="file.id">
          <div class="file-accept">
            <input type="checkbox" :value="file.id" v-model="selectedFiles" />
          </div>
          <div class="file-left">
            <span class="icon">{{ fileIcon(file.contentType) }}</span>
            <span class="name">{{ shortenString(file.originalFileName) }}</span>
          </div>
          <div class="file-size">{{ formatFileSize(file.fileSize) }}</div>

          <!-- Classification read-only columns -->
          <div class="classification-info">
            <span class="cl-chip" :class="`cl-${(file.status ?? 'Unclassified').toLowerCase()}`">
              {{ file.status ?? 'Unclassified' }}
            </span>
            <span v-if="file.markedValue" class="cl-field">M: {{ file.markedValue }} <small>({{ formatClassificationDate(file.markedAt) }})</small></span>
            <span v-if="file.enteredValue" class="cl-field">E: {{ file.enteredValue }} <small>({{ formatClassificationDate(file.enteredAt) }})</small></span>
            <span v-if="file.description" class="cl-field cl-desc" :title="file.description">{{ file.description }}</span>
          </div>

          <div class="file-actions">
            <button @click="openPreview(file)">View</button>
            <button @click="downloadFile(file)">Download</button>
            <button
              class="remove-file-btn"
              :disabled="file.enteredValue != null"
              :title="file.enteredValue != null ? 'Entered exhibits cannot be removed' : 'Remove this exhibit'"
              @click="removeExhibit(file)"
            >Remove</button>
          </div>
        </div>
      </div>

      <p v-if="removeError" class="remove-error">{{ removeError }}</p>

      <div class="actions-main">
        <button class="accept" @click="acceptSubmission">Accept Selected</button>
        <button class="remove" @click="showRejectModal = true">Reject / Delete All</button>
      </div>
      <p v-if="acceptError" class="accept-error">{{ acceptError }}</p>
    </div>

    <AppModal
      v-if="showRejectModal"
      title="Reject Submissions"
      confirm-label="Reject / Delete All"
      :confirm-danger="true"
      @confirm="showRejectModal = false; removeSubmission()"
      @cancel="showRejectModal = false"
    >
      Reject and delete these submissions? Any unaccepted files will be permanently removed.
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

