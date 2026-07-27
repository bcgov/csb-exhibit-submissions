<script setup lang="ts">
import {
  CLASSIFICATION_EDIT_WINDOW_SECONDS,
  ENTERED_MAX,
  ENTERED_MIN,
  EVIDENCE_SOURCE_TYPES,
  MARKED_MIN,
  SAVE_INDICATOR_FADE_SECONDS,
  VIEWABLE_CONTENT_TYPE_PREFIXES,
} from '@/constants/classification';
import { formatDateTime } from '@/helpers/formatters';
import type { SubmissionFile } from '@/models/SubmissionReviewModel';
import { mdiEyeOutline, mdiTrashCanOutline, mdiTrayArrowDown } from '@mdi/js';
import { computed, reactive, watch } from 'vue';
import ExhibitDescriptionCell from './ExhibitDescriptionCell.vue';

interface PriorFileEntry {
  file: SubmissionFile;
  submissionDate?: string;
  fileNumbers: string[];
}

const props = defineProps<{
  entries: PriorFileEntry[];
  markFn: (fileId: string, value: string) => Promise<SubmissionFile>;
  enterFn: (fileId: string, value: string) => Promise<SubmissionFile>;
  /** Appends the first description entry. Addenda are added from the detail modal. */
  addDescriptionFn: (fileId: string, text: string) => Promise<SubmissionFile>;
  evidenceSourceFn: (fileId: string, value: string) => Promise<SubmissionFile>;
  /** When true, all classification controls are always enabled (admin mode: no windows, no locks). */
  alwaysEditable?: boolean;
  /** When true, Removed exhibits are shown greyed-out with no controls. Default: hidden. */
  showRemoved?: boolean;
  /** Show a Download button for each non-Removed file. */
  canDownload?: boolean;
  /** Show a Remove button for each non-Removed file. */
  canRemove?: boolean;
  /**
   * When true, the filename renders as a button that emits `titleClick`, letting the
   * parent open its own exhibit-detail view. Default: plain, non-interactive text.
   */
  linkableTitle?: boolean;
  /**
   * Row-2 (Marked/Entered/Source) state a row starts in. Condensed by default — one
   * line per exhibit, which is what keeps a long Exhibit Search result set readable.
   */
  initialExpanded?: boolean;
}>();

const emit = defineEmits<{
  fileUpdated: [file: SubmissionFile];
  previewFile: [file: SubmissionFile];
  downloadFile: [file: SubmissionFile];
  removeFile: [file: SubmissionFile];
  titleClick: [file: SubmissionFile];
}>();

const markedWindowActive = reactive<Set<string>>(new Set());
const enteredWindowActive = reactive<Set<string>>(new Set());
const saveIndicators = reactive<Record<string, 'success' | string | null>>({});

// Per-row expansion of the classification controls. Seeded from `initialExpanded` as
// each entry appears; toggling afterwards is purely local.
const expandedRows = reactive<Set<string>>(new Set());
const seenRows = reactive<Set<string>>(new Set());

watch(
  () => props.entries,
  (entries) => {
    for (const entry of entries) {
      if (seenRows.has(entry.file.id)) continue;
      seenRows.add(entry.file.id);
      if (props.initialExpanded) expandedRows.add(entry.file.id);
    }
  },
  { immediate: true, deep: false },
);

const isExpanded = (fileId: string): boolean => expandedRows.has(fileId);

const toggleRow = (fileId: string) => {
  if (expandedRows.has(fileId)) expandedRows.delete(fileId);
  else expandedRows.add(fileId);
};

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

// May *append* a description. Entered locks officers out; admin keeps the input.
const isDescriptionEnabled = (file: SubmissionFile): boolean =>
  !!props.alwaysEditable || file.enteredValue == null;

const isEvidenceSourceEnabled = (file: SubmissionFile): boolean =>
  !!props.alwaysEditable || file.enteredValue == null;

const statusChipClass = (status?: string) => {
  if (status === 'Entered') return 'chip chip-entered';
  if (status === 'Marked') return 'chip chip-marked';
  if (status === 'Removed') return 'chip chip-unclassified';
  return 'chip chip-unclassified';
};

// "Exhibit" is the JJ-facing label for the Entered state; the chip also carries the
// assigned Marked letter / Exhibit number once one has been recorded.
const statusChipText = (file: SubmissionFile): string => {
  if (file.status === 'Marked') return file.markedValue ? `Marked ${file.markedValue}` : 'Marked';
  if (file.status === 'Entered')
    return file.enteredValue ? `Exhibit ${file.enteredValue}` : 'Exhibit';
  if (file.status === 'Removed') return 'Removed';
  return 'Unclassified';
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

// Adds the first description entry. Returns false so the cell keeps the draft text
// when the save fails, rather than silently losing what was typed.
const onDescriptionSave = async (file: SubmissionFile, text: string): Promise<boolean> => {
  try {
    const updated = await props.addDescriptionFn(file.id, text);
    emit('fileUpdated', updated);
    showSaveSuccess(file.id);
    return true;
  } catch (err: unknown) {
    const msg = err instanceof Error ? err.message : 'Failed to save description.';
    showSaveError(file.id, msg);
    return false;
  }
};

const onEvidenceSourceChange = async (file: SubmissionFile, value: string) => {
  try {
    const updated = await props.evidenceSourceFn(file.id, value);
    emit('fileUpdated', updated);
    showSaveSuccess(file.id);
  } catch (err: unknown) {
    const msg = err instanceof Error ? err.message : 'Failed to save source type.';
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
      :class="{
        'prior-file-item-removed': entry.file.status === 'Removed',
        'prior-file-item--condensed': !isExpanded(entry.file.id),
      }"
    >
      <!-- Row 1 — identical in both states: chevron, status, name, date, tickets, actions -->
      <div class="prior-file-row1">
        <button
          v-if="entry.file.status !== 'Removed'"
          type="button"
          class="btn btn--icon btn--tertiary chevron-btn"
          :aria-expanded="isExpanded(entry.file.id)"
          :aria-controls="`exhibit-row2-${entry.file.id}`"
          :title="isExpanded(entry.file.id) ? 'Hide classification' : 'Show classification'"
          :aria-label="isExpanded(entry.file.id) ? 'Hide classification' : 'Show classification'"
          @click="toggleRow(entry.file.id)"
        >
          {{ isExpanded(entry.file.id) ? '▾' : '▸' }}
        </button>
        <span :class="statusChipClass(entry.file.status)">{{ statusChipText(entry.file) }}</span>
        <button
          v-if="linkableTitle"
          type="button"
          class="prior-file-name prior-file-name-link"
          @click="emit('titleClick', entry.file)"
        >
          {{ entry.file.originalFileName }}
        </button>
        <span v-else class="prior-file-name">{{ entry.file.originalFileName }}</span>
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

        <!-- Save indicator: non-Removed files only -->
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
        </template>

        <!-- Action column: always rendered, even for Removed rows and files with no
             applicable action. Its fixed width is what keeps the date and ticket chips
             to its left aligned down the whole list. Icons carry the filename in their
             aria-label — rows repeat, so a bare "Download" would be ambiguous. -->
        <div class="view-container">
          <template v-if="entry.file.status !== 'Removed'">
            <button
              v-if="isViewable(entry.file.contentType)"
              type="button"
              class="btn btn--icon btn--primary-outline view-btn"
              :aria-label="`View ${entry.file.originalFileName}`"
              title="View"
              @click="emit('previewFile', entry.file)"
            >
              <svg class="action-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
                <path :d="mdiEyeOutline" />
              </svg>
            </button>
            <button
              v-if="canDownload"
              type="button"
              class="btn btn--icon btn--primary-outline dl-btn"
              :aria-label="`Download ${entry.file.originalFileName}`"
              title="Download"
              @click="emit('downloadFile', entry.file)"
            >
              <svg class="action-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
                <path :d="mdiTrayArrowDown" />
              </svg>
            </button>
            <button
              v-if="canRemove"
              type="button"
              class="btn btn--icon btn--danger-outline rm-btn"
              :aria-label="`Remove ${entry.file.originalFileName}`"
              title="Remove"
              @click="emit('removeFile', entry.file)"
            >
              <svg class="action-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
                <path :d="mdiTrashCanOutline" />
              </svg>
            </button>
          </template>
        </div>
      </div>

      <!-- Condensed: the description sits on its own tight line, aligned under the filename -->
      <div
        v-if="entry.file.status !== 'Removed' && !isExpanded(entry.file.id)"
        class="prior-file-desc-line"
      >
        <ExhibitDescriptionCell
          :file="entry.file"
          compact
          :disabled="!isDescriptionEnabled(entry.file)"
          :save-fn="(text: string) => onDescriptionSave(entry.file, text)"
        />
      </div>

      <!-- Expanded: Marked / Entered / Source / Description -->
      <div
        v-if="entry.file.status !== 'Removed' && isExpanded(entry.file.id)"
        :id="`exhibit-row2-${entry.file.id}`"
        class="prior-file-row2"
      >
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
          <span class="timestamp-text">
            {{ entry.file.markedAt ? formatClassificationDate(entry.file.markedAt) : '' }}
          </span>
        </div>

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
          <span class="timestamp-text">
            {{ entry.file.enteredAt ? formatClassificationDate(entry.file.enteredAt) : '' }}
          </span>
        </div>

        <div class="source-group">
          <label>Source</label>
          <select
            :disabled="!isEvidenceSourceEnabled(entry.file)"
            :value="entry.file.evidenceSourceType ?? ''"
            @change="onEvidenceSourceChange(entry.file, ($event.target as HTMLSelectElement).value)"
          >
            <option value="">—</option>
            <option v-for="opt in EVIDENCE_SOURCE_TYPES" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </option>
          </select>
        </div>

        <!-- Description (append-only: input only until the first entry exists) -->
        <ExhibitDescriptionCell
          :file="entry.file"
          :disabled="!isDescriptionEnabled(entry.file)"
          :save-fn="(text: string) => onDescriptionSave(entry.file, text)"
        />
      </div>
    </li>
  </ul>
</template>
