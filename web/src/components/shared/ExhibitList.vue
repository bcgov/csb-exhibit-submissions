<script setup lang="ts">
import {
  CLASSIFICATION_EDIT_WINDOW_SECONDS,
  DESCRIPTION_MAX_LENGTH,
  ENTERED_MAX,
  ENTERED_MIN,
  MARKED_MIN,
  SAVE_INDICATOR_FADE_SECONDS,
  VIEWABLE_CONTENT_TYPE_PREFIXES,
} from '@/constants/classification';
import { formatDateTime } from '@/helpers/formatters';
import type { ExhibitHistoryEntry, SubmissionFile } from '@/models/SubmissionReviewModel';
import useSubmissionService from '@/services/SubmissionService';
import { computed, reactive, ref, watch } from 'vue';

interface PriorFileEntry {
  file: SubmissionFile;
  submissionDate?: string;
  fileNumbers: string[];
}

const props = defineProps<{
  entries: PriorFileEntry[];
  markFn: (fileId: string, value: string) => Promise<SubmissionFile>;
  enterFn: (fileId: string, value: string) => Promise<SubmissionFile>;
  descriptionFn: (fileId: string, description: string) => Promise<SubmissionFile>;
  /** When true, all classification controls are always enabled (admin mode: no windows, no locks). */
  alwaysEditable?: boolean;
  /** When true, Removed exhibits are shown greyed-out with no controls. Default: hidden. */
  showRemoved?: boolean;
  /** Show a Download button for each non-Removed file. */
  canDownload?: boolean;
  /** Show a Remove button for each non-Removed file. */
  canRemove?: boolean;
}>();

const emit = defineEmits<{
  fileUpdated: [file: SubmissionFile];
  previewFile: [file: SubmissionFile];
  downloadFile: [file: SubmissionFile];
  removeFile: [file: SubmissionFile];
}>();

const { getFileHistory } = useSubmissionService();

// Exhibit change-history popup state
const historyFile = ref<SubmissionFile | null>(null);
const historyEntries = ref<ExhibitHistoryEntry[]>([]);
const historyLoading = ref(false);
const historyError = ref(false);

const HISTORY_FIELD_LABELS: Record<string, string> = {
  MarkedValue: 'Marked',
  EnteredValue: 'Entered',
  Description: 'Description',
};

const historyFieldLabel = (fieldName: string): string =>
  HISTORY_FIELD_LABELS[fieldName] ?? fieldName;

const openHistory = async (file: SubmissionFile) => {
  historyFile.value = file;
  historyEntries.value = [];
  historyError.value = false;
  historyLoading.value = true;
  try {
    historyEntries.value = await getFileHistory(file.id);
  } catch {
    historyError.value = true;
  } finally {
    historyLoading.value = false;
  }
};

const closeHistory = () => {
  historyFile.value = null;
};

const markedWindowActive = reactive<Set<string>>(new Set());
const enteredWindowActive = reactive<Set<string>>(new Set());
const saveIndicators = reactive<Record<string, 'success' | string | null>>({});
const localDescriptions = reactive<Record<string, string>>({});

watch(
  () => props.entries,
  (entries) => {
    for (const entry of entries) {
      if (!(entry.file.id in localDescriptions)) {
        localDescriptions[entry.file.id] = entry.file.description ?? '';
      }
    }
  },
  { immediate: true },
);

const visibleEntries = computed(() =>
  props.showRemoved ? props.entries : props.entries.filter((e) => e.file.status !== 'Removed'),
);

const markedLetters = Array.from({ length: 26 }, (_, i) =>
  String.fromCharCode(MARKED_MIN.charCodeAt(0) + i),
);
const enteredNumbers = Array.from({ length: ENTERED_MAX - ENTERED_MIN + 1 }, (_, i) =>
  String(ENTERED_MIN + i),
);

const isViewable = (contentType: string): boolean =>
  VIEWABLE_CONTENT_TYPE_PREFIXES.some((prefix) => contentType.startsWith(prefix));

const isMarkedEnabled = (file: SubmissionFile): boolean => {
  if (props.alwaysEditable) return true;
  if (file.enteredValue != null) return false;
  if (file.markedValue == null) return true;
  return markedWindowActive.has(file.id);
};

const isEnteredEnabled = (file: SubmissionFile): boolean => {
  if (props.alwaysEditable) return true;
  if (file.enteredValue == null) return true;
  return enteredWindowActive.has(file.id);
};

const isDescriptionEnabled = (file: SubmissionFile): boolean =>
  !!props.alwaysEditable || file.enteredValue == null;

const statusChipClass = (status?: string) => {
  if (status === 'Entered') return 'chip chip-entered';
  if (status === 'Marked') return 'chip chip-marked';
  if (status === 'Removed') return 'chip chip-unclassified';
  return 'chip chip-unclassified';
};

const formatClassificationDate = (iso?: string | null): string => {
  if (!iso) return '';
  return formatDateTime(iso, true);
};

const showSaveSuccess = (fileId: string) => {
  saveIndicators[fileId] = 'success';
  setTimeout(() => {
    if (saveIndicators[fileId] === 'success') saveIndicators[fileId] = null;
  }, SAVE_INDICATOR_FADE_SECONDS * 1000);
};

const showSaveError = (fileId: string, message: string) => {
  saveIndicators[fileId] = message;
};

const startMarkedWindow = (fileId: string) => {
  markedWindowActive.add(fileId);
  setTimeout(() => markedWindowActive.delete(fileId), CLASSIFICATION_EDIT_WINDOW_SECONDS * 1000);
};

const startEnteredWindow = (fileId: string) => {
  enteredWindowActive.add(fileId);
  setTimeout(() => enteredWindowActive.delete(fileId), CLASSIFICATION_EDIT_WINDOW_SECONDS * 1000);
};

const onMarkChange = async (file: SubmissionFile, value: string) => {
  if (!value) return;
  try {
    const updated = await props.markFn(file.id, value);
    emit('fileUpdated', updated);
    startMarkedWindow(file.id);
    showSaveSuccess(file.id);
  } catch (err: unknown) {
    const msg = err instanceof Error ? err.message : 'Failed to mark exhibit.';
    showSaveError(file.id, msg);
  }
};

const onEnterChange = async (file: SubmissionFile, value: string) => {
  if (!value) return;
  try {
    const updated = await props.enterFn(file.id, value);
    emit('fileUpdated', updated);
    // Clear Marked window immediately — only Entered is correctable within its own window
    markedWindowActive.delete(file.id);
    startEnteredWindow(file.id);
    showSaveSuccess(file.id);
  } catch (err: unknown) {
    const msg = err instanceof Error ? err.message : 'Failed to enter exhibit.';
    showSaveError(file.id, msg);
  }
};

const onDescriptionBlur = async (file: SubmissionFile) => {
  const description = localDescriptions[file.id] ?? '';
  if (description === (file.description ?? '')) return;
  try {
    const updated = await props.descriptionFn(file.id, description);
    emit('fileUpdated', updated);
    showSaveSuccess(file.id);
  } catch (err: unknown) {
    const msg = err instanceof Error ? err.message : 'Failed to save description.';
    showSaveError(file.id, msg);
  }
};
</script>

<template>
  <ul class="prior-file-list">
    <li
      v-for="entry in visibleEntries"
      :key="entry.file.id"
      class="prior-file-item"
      :class="{ 'prior-file-item-removed': entry.file.status === 'Removed' }"
    >
      <!-- Row 1: name, (date), (ticket badge), status chip, (save indicator), (actions) -->
      <div class="prior-file-row1">
        <button
          type="button"
          class="btn btn--icon btn--tertiary history-btn"
          title="View change history"
          aria-label="View change history"
          @click="openHistory(entry.file)"
        >
          🕑
        </button>
        <span class="prior-file-name">{{ entry.file.originalFileName }}</span>
        <span v-if="entry.submissionDate" class="prior-file-date">
          {{ formatDateTime(entry.submissionDate, true) }}
        </span>
        <span v-if="entry.fileNumbers.length > 0" class="prior-file-tickets">
          File #{{
            entry.fileNumbers.length <= 2 ? entry.fileNumbers.join(', ') : entry.fileNumbers[0]
          }}<span
            v-if="entry.fileNumbers.length > 2"
            class="ticket-overflow"
            :title="entry.fileNumbers.join(' \n')"
          >
            (+{{ entry.fileNumbers.length - 1 }})</span
          >
        </span>
        <span :class="statusChipClass(entry.file.status)">{{
          entry.file.status ?? 'Unclassified'
        }}</span>

        <!-- Save indicator and action buttons: non-Removed files only -->
        <template v-if="entry.file.status !== 'Removed'">
          <span
            v-if="saveIndicators[entry.file.id] === 'success'"
            class="save-indicator save-success"
            title="Saved"
            >✓</span
          >
          <span
            v-else-if="saveIndicators[entry.file.id]"
            class="save-indicator save-error"
            :title="saveIndicators[entry.file.id] as string"
            >✕</span
          >

          <div class="view-container">
            <button
              v-if="isViewable(entry.file.contentType)"
              type="button"
              class="btn btn--sm btn--primary-outline view-btn"
              @click="emit('previewFile', entry.file)"
            >
              View
            </button>
            <button
              v-if="canDownload"
              type="button"
              class="btn btn--sm btn--primary-outline dl-btn"
              @click="emit('downloadFile', entry.file)"
            >
              Download
            </button>
            <button
              v-if="canRemove"
              type="button"
              class="btn btn--sm btn--danger-outline rm-btn"
              @click="emit('removeFile', entry.file)"
            >
              Remove
            </button>
          </div>
        </template>
      </div>

      <!-- Row 2: classification controls (non-Removed files only) -->
      <div v-if="entry.file.status !== 'Removed'" class="prior-file-row2">
        <!-- Marked -->
        <div class="classification-group">
          <label>Marked</label>
          <select
            :disabled="!isMarkedEnabled(entry.file)"
            :value="entry.file.markedValue ?? ''"
            @change="onMarkChange(entry.file, ($event.target as HTMLSelectElement).value)"
          >
            <option value="">—</option>
            <option v-for="letter in markedLetters" :key="letter" :value="letter">
              {{ letter }}
            </option>
          </select>
          <span v-if="entry.file.markedAt" class="timestamp-text">
            {{ formatClassificationDate(entry.file.markedAt) }}
          </span>
        </div>

        <!-- Entered -->
        <div class="classification-group">
          <label>Entered</label>
          <select
            :disabled="!isEnteredEnabled(entry.file)"
            :value="entry.file.enteredValue ?? ''"
            @change="onEnterChange(entry.file, ($event.target as HTMLSelectElement).value)"
          >
            <option value="">—</option>
            <option v-for="num in enteredNumbers" :key="num" :value="num">{{ num }}</option>
          </select>
          <span v-if="entry.file.enteredAt" class="timestamp-text">
            {{ formatClassificationDate(entry.file.enteredAt) }}
          </span>
        </div>

        <!-- Description -->
        <div class="description-group">
          <label>Description</label>
          <input
            type="text"
            :disabled="!isDescriptionEnabled(entry.file)"
            :maxlength="DESCRIPTION_MAX_LENGTH"
            v-model="localDescriptions[entry.file.id]"
            @blur="onDescriptionBlur(entry.file)"
            placeholder="Optional description…"
          />
          <span
            class="desc-counter"
            :class="{
              over: (localDescriptions[entry.file.id]?.length ?? 0) > DESCRIPTION_MAX_LENGTH,
            }"
          >
            {{ DESCRIPTION_MAX_LENGTH - (localDescriptions[entry.file.id]?.length ?? 0) }} remaining
          </span>
        </div>
      </div>
    </li>
  </ul>

  <!-- Per-exhibit change history popup -->
  <div v-if="historyFile" class="exhibit-history-overlay" @click.self="closeHistory">
    <div class="exhibit-history-dialog" role="dialog" aria-modal="true">
      <h3>Change History — {{ historyFile.originalFileName }}</h3>

      <p v-if="historyLoading" class="history-status">Loading…</p>
      <p v-else-if="historyError" class="history-status history-error">
        Could not load change history. Please try again.
      </p>
      <table v-else-if="historyEntries.length > 0" class="exhibit-history-table">
        <thead>
          <tr>
            <th>Field</th>
            <th>From</th>
            <th>To</th>
            <th>Changed By</th>
            <th>When</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(item, idx) in historyEntries" :key="idx">
            <td>{{ historyFieldLabel(item.fieldName) }}</td>
            <td>{{ item.oldValue ?? '—' }}</td>
            <td>{{ item.newValue ?? '—' }}</td>
            <td>{{ item.changedBy ?? '—' }}</td>
            <td>{{ formatDateTime(item.changedAtUTC, true) }}</td>
          </tr>
        </tbody>
      </table>
      <p v-else class="history-status">No changes have been recorded for this exhibit.</p>

      <div class="exhibit-history-footer">
        <button type="button" class="btn btn--secondary" @click="closeHistory">Close</button>
      </div>
    </div>
  </div>
</template>
