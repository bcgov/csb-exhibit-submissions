<script setup lang="ts">
import { convertUtcToLocal, formatDateTime, splitDateTimeForDisplay } from '@/helpers/formatters';
import type {
  SubmissionActionModel,
  SubmissionFile,
  SubmissionReviewModel,
} from '@/models/SubmissionReviewModel';
import useSubmissionService from '@/services/SubmissionService';
import { computed, onMounted, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import AppModal from '../shared/AppModal.vue';
import ExhibitList from '../shared/ExhibitList.vue';
import FileViewer from '../shared/FileViewer.vue';

const route = useRoute();
const router = useRouter();

const submissionId = Number(route.params.id);

const {
  retrieveSubmission,
  rejectSubmission,
  removeFile,
  markExhibit,
  enterExhibit,
  updateExhibitDescription,
  updateEvidenceSource,
} = useSubmissionService();

const submission = ref<SubmissionReviewModel | undefined>(undefined);
const showRejectModal = ref(false);
const removeError = ref<string | null>(null);
const previewFile = ref<SubmissionFile | null>(null);

const getFileUrl = (fileId: string, action: 'view' | 'download') =>
  `/api/files/${fileId}/${action}`;

const exhibitEntries = computed(() =>
  (submission.value?.files ?? []).map((f) => ({ file: f, fileNumbers: [] as string[] })),
);

// Only Rejected is truly terminal. Accepted is now a derived, reversible state
// (adding a file reopens it, and per-file Entered-locking guards immutability),
// so the review stays editable/rejectable while Accepted (CES-39).
const isTerminal = computed(() => submission.value?.status === 'Rejected');

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

const updateFileInSubmission = (updated: SubmissionFile) => {
  if (!submission.value) return;
  submission.value = {
    ...submission.value,
    files: submission.value.files.map((f) =>
      f.id === updated.id
        ? { ...f, ...updated, viewUrl: f.viewUrl, downloadUrl: f.downloadUrl }
        : f,
    ),
  };
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
      files: submission.value.files.map((f) =>
        f.id === file.id ? { ...f, status: 'Removed', deletedAt: new Date().toISOString() } : f,
      ),
    };
  } else if (!success) {
    removeError.value = 'Could not remove exhibit.';
  }
};
</script>

<template>
  <div class="review-page">
    <button class="btn btn--tertiary back-button" @click="router.push('/admin/list')">
      ← Back to Submissions
    </button>

    <h1>Submission Review</h1>

    <div v-if="submission">
      <div class="details-grid">
        <div>
          <strong>Court Date:</strong> {{ splitDateTimeForDisplay(submission.courtDateTime).date }}
        </div>
        <div>
          <strong>Court Time:</strong> {{ splitDateTimeForDisplay(submission.courtDateTime).time }}
        </div>
        <div><strong>Location:</strong> {{ submission.location }}</div>
        <div><strong>Room:</strong> {{ submission.room }}</div>
        <div>
          <strong>Submission Date:</strong>
          {{
            submission.submissionDate
              ? formatDateTime(convertUtcToLocal(submission.submissionDate), true)
              : ''
          }}
        </div>
        <div class="status-cell">
          <strong>Status:</strong>
          <span :class="`status-chip status-${submission.status.toLowerCase()}`">{{
            submission.status
          }}</span>
        </div>
      </div>

      <!-- Tickets section -->
      <h2>Tickets ({{ submission.tickets?.length ?? 0 }})</h2>
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

      <h2>Submitted Evidence</h2>

      <ExhibitList
        :entries="exhibitEntries"
        :always-editable="!isTerminal"
        :show-removed="true"
        :can-download="true"
        :can-remove="!isTerminal"
        :mark-fn="(id: string, v: string) => markExhibit(id, { markedValue: v })"
        :enter-fn="(id: string, v: string) => enterExhibit(id, { enteredValue: v })"
        :description-fn="
          (id: string, d: string) => updateExhibitDescription(id, { description: d })
        "
        :evidence-source-fn="
          (id: string, v: string) => updateEvidenceSource(id, { evidenceSourceType: v })
        "
        @file-updated="updateFileInSubmission"
        @preview-file="openPreview"
        @download-file="downloadFile"
        @remove-file="removeExhibit"
      />

      <p v-if="removeError" class="remove-error">{{ removeError }}</p>

      <!-- Actions: reject is available until the submission is terminally Rejected.
           Whole-submission Accept is retired — status derives from per-file acceptance. -->
      <template v-if="!isTerminal">
        <div class="actions-main">
          <button class="btn btn--danger remove" @click="showRejectModal = true">
            Reject Submission
          </button>
        </div>
      </template>
    </div>

    <!-- Reject confirmation modal -->
    <AppModal
      v-if="showRejectModal"
      title="Reject Submission"
      confirm-label="Reject Submission"
      :confirm-danger="true"
      @confirm="
        showRejectModal = false;
        doRejectSubmission();
      "
      @cancel="showRejectModal = false"
    >
      Rejecting this submission permanently deletes <strong>all</strong> associated files. This
      cannot be undone and the files are unretrievable.
    </AppModal>

    <div v-if="previewFile" class="preview-modal">
      <div class="modal-content">
        <button
          class="btn btn--icon btn--tertiary close"
          aria-label="Close preview"
          @click="closePreview"
        >
          ✖
        </button>
        <FileViewer
          :fileUrl="previewFile.viewUrl"
          :download-url="previewFile.downloadUrl"
          :mimeType="previewFile.contentType"
        />
      </div>
    </div>
  </div>
</template>
