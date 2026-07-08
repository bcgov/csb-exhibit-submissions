<script setup lang="ts">
import { FILE_NUMBER_MIN_LENGTH } from '@/constants/submission';
import type {
  ExhibitSearchFilter,
  ExhibitSearchResultModel,
} from '@/models/ExhibitSearchResultModel';
import type { SubmissionFile } from '@/models/SubmissionReviewModel';
import useSubmissionService from '@/services/SubmissionService';
import type { AxiosError } from 'axios';
import { computed, reactive, ref } from 'vue';
import ExhibitDetailModal from './ExhibitDetailModal.vue';
import ExhibitList from '../shared/ExhibitList.vue';
import FileViewer from '../shared/FileViewer.vue';

const { searchExhibits, markExhibit, enterExhibit, updateExhibitDescription } =
  useSubmissionService();

const filter = reactive<ExhibitSearchFilter>({
  fileNumberText: '',
  accusedName: '',
  appearanceDateFrom: '',
  appearanceDateTo: '',
});

const results = ref<ExhibitSearchResultModel[]>([]);
const loading = ref(false);
const searched = ref(false);
const errorMessage = ref<string | null>(null);
const previewFile = ref<SubmissionFile | null>(null);
const detailResult = ref<ExhibitSearchResultModel | null>(null);

const getFileUrl = (fileId: string, action: 'view' | 'download') =>
  `/api/files/${fileId}/${action}`;

// A file number, once entered, must reach the minimum length; a last name alone is
// always a valid term. Mirrors the backend's BadRequest guards.
const fileNumberTrimmed = computed(() => filter.fileNumberText?.trim() ?? '');
const accusedNameTrimmed = computed(() => filter.accusedName?.trim() ?? '');
const fileNumberTooShort = computed(
  () =>
    fileNumberTrimmed.value.length > 0 &&
    fileNumberTrimmed.value.length < FILE_NUMBER_MIN_LENGTH,
);
const canSearch = computed(
  () =>
    !fileNumberTooShort.value &&
    (fileNumberTrimmed.value.length >= FILE_NUMBER_MIN_LENGTH ||
      accusedNameTrimmed.value.length > 0),
);

const validationHint = computed(() => {
  if (fileNumberTooShort.value)
    return `File number must be at least ${FILE_NUMBER_MIN_LENGTH} characters.`;
  return `Enter a file number (${FILE_NUMBER_MIN_LENGTH}+ characters) or accused name to search.`;
});

// ExhibitList consumes PriorFileEntry ({ file, submissionDate?, fileNumbers[] }); each
// result is a superset, so map directly while preserving the backend's sorted order.
const exhibitEntries = computed(() =>
  results.value.map((r) => ({
    file: r.file,
    submissionDate: r.submissionDate,
    fileNumbers: r.fileNumbers,
  })),
);

const runSearch = async () => {
  if (!canSearch.value) {
    searched.value = false;
    return;
  }

  loading.value = true;
  errorMessage.value = null;
  try {
    const data = await searchExhibits({ ...filter });
    results.value = data.map((r) => ({
      ...r,
      file: {
        ...r.file,
        viewUrl: getFileUrl(r.file.id, 'view'),
        downloadUrl: getFileUrl(r.file.id, 'download'),
      },
    }));
    searched.value = true;
  } catch (err: unknown) {
    results.value = [];
    searched.value = true;
    const axiosErr = err as AxiosError<unknown>;
    if (axiosErr.isAxiosError) {
      if (axiosErr.response?.status === 403) {
        errorMessage.value = 'You do not have permission to view this data.';
      } else if (axiosErr.response?.status === 400) {
        errorMessage.value =
          typeof axiosErr.response.data === 'string'
            ? axiosErr.response.data
            : 'Please provide a valid file number or accused name.';
      } else {
        errorMessage.value = 'Could not run the search. Please try again.';
      }
    } else {
      errorMessage.value = 'Could not run the search. Please try again.';
    }
  } finally {
    loading.value = false;
  }
};

const clearSearch = () => {
  filter.fileNumberText = '';
  filter.accusedName = '';
  filter.appearanceDateFrom = '';
  filter.appearanceDateTo = '';
  results.value = [];
  searched.value = false;
  errorMessage.value = null;
};

const openPreview = (file: SubmissionFile) => {
  previewFile.value = file;
};
const closePreview = () => {
  previewFile.value = null;
};

// Filename click on a result row → open the exhibit detail popup. The popup is owned
// here (not in the shared ExhibitList), fed the full result row for context.
const openDetails = (file: SubmissionFile) => {
  detailResult.value = results.value.find((r) => r.file.id === file.id) ?? null;
};
const closeDetails = () => {
  detailResult.value = null;
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

// Patch the matching row in place so the classification badge/state updates after an
// inline auto-save, preserving the client-attached view/download URLs.
const updateFileInResults = (updated: SubmissionFile) => {
  results.value = results.value.map((r) =>
    r.file.id === updated.id
      ? { ...r, file: { ...r.file, ...updated, viewUrl: r.file.viewUrl, downloadUrl: r.file.downloadUrl } }
      : r,
  );
};
</script>

<template>
  <div class="exhibit-search-page">
    <h1>Exhibit Search</h1>

    <div v-if="errorMessage" class="alert alert-danger">{{ errorMessage }}</div>

    <!-- Search form -->
    <form class="filter-panel" @submit.prevent="runSearch">
      <div class="filter-row">
        <label>
          File #
          <input
            type="text"
            v-model="filter.fileNumberText"
            placeholder="e.g. AH123456789-1"
          />
        </label>
        <label>
          Last name
          <input type="text" v-model="filter.accusedName" placeholder="last name" />
        </label>
        <label>
          Court date from
          <input type="date" v-model="filter.appearanceDateFrom" />
        </label>
        <label>
          Court date to
          <input type="date" v-model="filter.appearanceDateTo" />
        </label>
      </div>
      <p class="helper-text">Enter file number or accused name to get exhibit list</p>
      <div class="filter-actions">
        <button type="submit" class="btn btn--primary btn-search" :disabled="!canSearch">
          Search
        </button>
        <button type="button" class="btn btn--secondary btn-clear" @click="clearSearch">
          Clear
        </button>
      </div>
      <p class="validation-hint">{{ canSearch ? ' ' : validationHint }}</p>
    </form>

    <p v-if="loading" class="loading-text">Loading…</p>

    <template v-else-if="searched">
      <ExhibitList
        v-if="results.length > 0"
        :entries="exhibitEntries"
        :always-editable="true"
        :show-removed="false"
        :can-download="true"
        :can-remove="false"
        :linkable-title="true"
        :mark-fn="(id: string, v: string) => markExhibit(id, { markedValue: v })"
        :enter-fn="(id: string, v: string) => enterExhibit(id, { enteredValue: v })"
        :description-fn="(id: string, d: string) => updateExhibitDescription(id, { description: d })"
        @file-updated="updateFileInResults"
        @preview-file="openPreview"
        @download-file="downloadFile"
        @title-click="openDetails"
      />
      <p v-else class="empty-state">No exhibits found for this search.</p>
    </template>

    <!-- Preview modal -->
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

    <!-- Exhibit detail popup (read-only details, change history, registry notes) -->
    <ExhibitDetailModal v-if="detailResult" :result="detailResult" @close="closeDetails" />
  </div>
</template>
