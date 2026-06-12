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

const { retrieveSubmission, acceptSubmissionFiles, rejectAndCloseSubmission } = useSubmissionService();

const submission = ref<SubmissionReviewModel | undefined>(undefined);
const selectedFiles = ref<string[]>([]);
const acceptError = ref<string | null>(null);
const showRejectModal = ref(false);

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

const fileIcon = (type: string) => {
  if (type.startsWith('image')) return '🖼';
  if (type.startsWith('video')) return '🎬';
  if (type.includes('pdf')) return '📄';
  return '📁';
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
          <div class="file-actions">
            <button @click="openPreview(file)">View</button>
            <button @click="downloadFile(file)">Download</button>
          </div>
        </div>
      </div>

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

<style scoped>
.review-page {
  padding: 2rem;
  max-width: 900px;
  margin: auto;
}

.details-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(275px, 1fr));
  gap: 10px;
  margin-bottom: 30px;
}

.ticket-table {
  width: 100%;
  border-collapse: collapse;
  margin-bottom: 1.5rem;
}

.ticket-table th,
.ticket-table td {
  border: 1px solid #ddd;
  padding: 0.6rem 0.75rem;
  font-size: 0.9rem;
}

.ticket-table thead {
  background: #f5f5f5;
}

.text-monospace {
  font-family: monospace;
}

.icon {
  font-size: 40px;
  margin-bottom: 5px;
}

.name {
  font-size: 0.9rem;
  margin-bottom: 8px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.actions button {
  margin: 3px;
}

.actions-main {
  margin-top: 30px;
  display: flex;
  gap: 10px;
}

.accept {
  background: #4caf50;
  color: white;
}

.remove {
  background: #e53935;
  color: white;
}

.preview-modal {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
}

.modal-content {
  background: white;
  padding: 20px;
  max-width: 1000px;
  width: 90%;
  max-height: 90vh;
  position: relative;
}

.modal-content img,
.modal-content video,
.modal-content iframe {
  max-width: 100%;
  max-height: 70vh;
}

.close {
  position: absolute;
  top: 5px;
  right: 5px;
}

.file-list {
  border: 1px solid #ddd;
  border-radius: 6px;
}

.file-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 14px;
  border-bottom: 1px solid #eee;
  column-gap: 20px;
}

.file-row:hover {
  background: #f7f7f7;
}

.file-row:last-child {
  border-bottom: none;
}

.file-left {
  display: flex;
  align-items: center;
  gap: 10px;
  flex: 1;
  min-width: 0;
}

.icon {
  font-size: 22px;
  width: 26px;
  text-align: center;
}

.name {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.file-actions {
  display: flex;
  gap: 8px;
}

.file-actions button {
  padding: 4px 10px;
  font-size: 0.85rem;
}

.accept-error {
  margin-top: 8px;
  color: #e53935;
  font-size: 0.9rem;
}
</style>
