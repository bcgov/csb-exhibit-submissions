<script setup lang="ts">
import { DESCRIPTION_MAX_LENGTH } from '@/constants/classification';
import { EXHIBIT_NOTE_MAX_LENGTH } from '@/constants/submission';
import { formatDateTime, formatFileSize, splitDateTimeForDisplay } from '@/helpers/formatters';
import type { ExhibitNoteModel } from '@/models/ExhibitNoteModel';
import type { ExhibitSearchResultModel } from '@/models/ExhibitSearchResultModel';
import type { ExhibitHistoryEntry, SubmissionFile } from '@/models/SubmissionReviewModel';
import useSubmissionService from '@/services/SubmissionService';
import { computed, onMounted, ref } from 'vue';

const props = defineProps<{
  result: ExhibitSearchResultModel;
  /**
   * Registry notes are JJ/registry-only (and Admin-only at the API). Officers open this
   * same modal without them — the section is neither rendered nor fetched.
   */
  canViewNotes?: boolean;
  /** Appends a description entry. Omit to render the description history read-only. */
  addDescriptionFn?: (fileId: string, text: string) => Promise<SubmissionFile>;
  /** Admin mode: appending stays available even once the exhibit is Entered. */
  alwaysEditable?: boolean;
}>();

const emit = defineEmits<{
  close: [];
  fileUpdated: [file: SubmissionFile];
}>();

const { getFileHistory, getExhibitNotes, addExhibitNote } = useSubmissionService();

// Local copy so an appended description is reflected immediately; the parent gets the
// same file back via `fileUpdated`.
const file = ref<SubmissionFile>(props.result.file);

// --- Change history ---
const history = ref<ExhibitHistoryEntry[]>([]);
const historyLoading = ref(false);
const historyError = ref(false);

// Description is no longer an audited field (CES-42) — its entry list is its history.
const HISTORY_FIELD_LABELS: Record<string, string> = {
  MarkedValue: 'Marked',
  EnteredValue: 'Entered',
  EvidenceSourceType: 'Source',
};
const historyFieldLabel = (fieldName: string): string =>
  HISTORY_FIELD_LABELS[fieldName] ?? fieldName;

// --- Description entries (append-only) ---
const descriptions = computed(() => file.value.descriptions ?? []);
const newDescription = ref('');
const savingDescription = ref(false);
const saveDescriptionError = ref<string | null>(null);

// Mirrors the API rule: officers cannot append once the exhibit is Entered; an admin
// (who is handed an addDescriptionFn with the override) still can.
const canAddDescription = computed(
  () =>
    props.addDescriptionFn != null && (!!props.alwaysEditable || file.value.enteredValue == null),
);

const canSaveDescription = computed(
  () =>
    newDescription.value.trim().length > 0 &&
    newDescription.value.length <= DESCRIPTION_MAX_LENGTH &&
    !savingDescription.value,
);

// --- Registry notes ---
const notes = ref<ExhibitNoteModel[]>([]);
const notesLoading = ref(false);
const notesError = ref(false);
const newNote = ref('');
const savingNote = ref(false);
const saveNoteError = ref<string | null>(null);

const canSaveNote = computed(
  () =>
    newNote.value.trim().length > 0 &&
    newNote.value.length <= EXHIBIT_NOTE_MAX_LENGTH &&
    !savingNote.value,
);

const appearance = computed(() =>
  props.result.appearanceDateTime
    ? splitDateTimeForDisplay(props.result.appearanceDateTime)
    : { date: '', time: '' },
);

onMounted(async () => {
  historyLoading.value = true;
  try {
    history.value = await getFileHistory(file.value.id);
  } catch {
    historyError.value = true;
  } finally {
    historyLoading.value = false;
  }

  // Admin-only endpoint — never called for an officer.
  if (!props.canViewNotes) return;
  notesLoading.value = true;
  try {
    notes.value = await getExhibitNotes(file.value.id);
  } catch {
    notesError.value = true;
  } finally {
    notesLoading.value = false;
  }
});

const saveDescription = async () => {
  if (!canSaveDescription.value || !props.addDescriptionFn) return;
  savingDescription.value = true;
  saveDescriptionError.value = null;
  try {
    const updated = await props.addDescriptionFn(file.value.id, newDescription.value.trim());
    file.value = updated;
    emit('fileUpdated', updated);
    newDescription.value = '';
  } catch {
    saveDescriptionError.value = 'Could not save the description. Please try again.';
  } finally {
    savingDescription.value = false;
  }
};

const saveNote = async () => {
  if (!canSaveNote.value) return;
  savingNote.value = true;
  saveNoteError.value = null;
  try {
    const created = await addExhibitNote(file.value.id, newNote.value.trim());
    notes.value = [...notes.value, created];
    newNote.value = '';
  } catch {
    saveNoteError.value = 'Could not save the note. Please try again.';
  } finally {
    savingNote.value = false;
  }
};
</script>

<template>
  <div class="exhibit-detail-overlay" @click.self="emit('close')">
    <div class="exhibit-detail-dialog" role="dialog" aria-modal="true" aria-label="Exhibit details">
      <header class="exhibit-detail-header">
        <h2>{{ file.originalFileName }}</h2>
        <button type="button" class="btn btn--icon btn--tertiary close" aria-label="Close exhibit details"
          @click="emit('close')">
          ✖
        </button>
      </header>

      <!-- Submission info -->
      <details class="detail-section" open>
        <summary>
          <h3>Submission</h3>
        </summary>
        <dl class="detail-grid">
          <div>
            <dt>Submission ID</dt>
            <dd>{{ result.submissionId }}</dd>
          </div>
          <div>
            <dt>Location</dt>
            <dd>{{ result.location || '—' }}</dd>
          </div>
          <div>
            <dt>Room</dt>
            <dd>{{ result.room || '—' }}</dd>
          </div>
          <div>
            <dt>Court date</dt>
            <dd>{{ appearance.date || '—' }}</dd>
          </div>
          <div>
            <dt>Court time</dt>
            <dd>{{ appearance.time || '—' }}</dd>
          </div>
          <div>
            <dt>Accused</dt>
            <dd>{{ result.accusedName || '—' }}</dd>
          </div>
          <div class="detail-grid__wide">
            <dt>File numbers</dt>
            <dd>{{ result.fileNumbers.join(', ') || '—' }}</dd>
          </div>
        </dl>
      </details>

      <!-- Exhibit info -->
      <details class="detail-section" open>
        <summary>
          <h3>Exhibit</h3>
        </summary>
        <dl class="detail-grid">
          <div>
            <dt>Status</dt>
            <dd>{{ file.status ?? 'Unclassified' }}</dd>
          </div>
          <div>
            <dt>Marked</dt>
            <dd>{{ file.markedValue ?? '—' }}</dd>
          </div>
          <div>
            <dt>Marked at</dt>
            <dd>{{ file.markedAt ? formatDateTime(file.markedAt, true) : '—' }}</dd>
          </div>
          <div>
            <dt>Source</dt>
            <dd>{{ file.evidenceSourceType ?? '—' }}</dd>
          </div>
          <div>
            <dt>Entered</dt>
            <dd>{{ file.enteredValue ?? '—' }}</dd>
          </div>
          <div>
            <dt>Entered at</dt>
            <dd>{{ file.enteredAt ? formatDateTime(file.enteredAt, true) : '—' }}</dd>
          </div>
        </dl>
      </details>

      <!-- Description entries (append-only; an addendum never replaces what came before) -->
      <section class="detail-section descriptions-section">
        <h3>Exhibit Description</h3>

        <ul v-if="descriptions.length > 0" class="entry-list">
          <li v-for="entry in descriptions" :key="entry.id" class="entry-item">
            <p class="entry-text">{{ entry.descriptionText }}</p>
            <p class="entry-meta">
              {{ entry.createdBy ?? '—' }} · {{ formatDateTime(entry.createdAtUTC, true) }}
            </p>
          </li>
        </ul>
        <p v-else class="detail-status">No description has been added yet.</p>

        <div v-if="canAddDescription" class="entry-add">
          <label for="new-description">Add a description</label>
          <textarea id="new-description" v-model="newDescription" :maxlength="DESCRIPTION_MAX_LENGTH" rows="3"
            placeholder="Add a description (saved permanently and cannot be edited)…"></textarea>
          <div class="entry-add-footer">
            <span class="entry-counter" :class="{ over: newDescription.length > DESCRIPTION_MAX_LENGTH }">
              {{ DESCRIPTION_MAX_LENGTH - newDescription.length }} remaining
            </span>
            <button type="button" class="btn btn--primary btn-save-description" :disabled="!canSaveDescription"
              @click="saveDescription">
              {{ savingDescription ? 'Saving…' : 'Save description' }}
            </button>
          </div>
          <p v-if="saveDescriptionError" class="detail-status detail-error">
            {{ saveDescriptionError }}
          </p>
        </div>
      </section>

      <!-- Registry-only notes (admin/JJ); collapsed by default since notes are rarely used. -->
      <details v-if="canViewNotes" class="detail-section notes-section">
        <summary>
          <div class="notes-heading">
            <h3>Notes</h3>
            <span class="registry-badge">Registry use only</span>
          </div>
        </summary>

        <p v-if="notesLoading" class="detail-status">Loading…</p>
        <p v-else-if="notesError" class="detail-status detail-error">Could not load notes.</p>
        <ul v-else-if="notes.length > 0" class="entry-list">
          <li v-for="note in notes" :key="note.id" class="entry-item">
            <p class="entry-text">{{ note.noteText }}</p>
            <p class="entry-meta">
              {{ note.createdBy ?? 'Registry' }} · {{ formatDateTime(note.createdAtUTC, true) }}
            </p>
          </li>
        </ul>
        <p v-else class="detail-status">No notes yet.</p>

        <!-- Add note (append-only; immutable once saved) -->
        <div class="entry-add">
          <label for="new-note">Add a note</label>
          <textarea id="new-note" v-model="newNote" :maxlength="EXHIBIT_NOTE_MAX_LENGTH" rows="3"
            placeholder="Add a registry note (saved permanently and cannot be edited)…"></textarea>
          <div class="entry-add-footer">
            <span class="entry-counter" :class="{ over: newNote.length > EXHIBIT_NOTE_MAX_LENGTH }">
              {{ EXHIBIT_NOTE_MAX_LENGTH - newNote.length }} remaining
            </span>
            <button type="button" class="btn btn--primary btn-save-note" :disabled="!canSaveNote" @click="saveNote">
              {{ savingNote ? 'Saving…' : 'Save note' }}
            </button>
          </div>
          <p v-if="saveNoteError" class="detail-status detail-error">{{ saveNoteError }}</p>
        </div>
      </details>

      <!-- Change history -->
      <details class="detail-section">
        <summary>
          <h3>Change History</h3>
        </summary>
        <p v-if="historyLoading" class="detail-status">Loading…</p>
        <p v-else-if="historyError" class="detail-status detail-error">
          Could not load change history.
        </p>
        <table v-else-if="history.length > 0" class="detail-history-table">
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
            <tr v-for="(item, idx) in history" :key="idx">
              <td>{{ historyFieldLabel(item.fieldName) }}</td>
              <td>{{ item.oldValue ?? '—' }}</td>
              <td>{{ item.newValue ?? '—' }}</td>
              <td>{{ item.changedBy ?? '—' }}</td>
              <td>{{ formatDateTime(item.changedAtUTC, true) }}</td>
            </tr>
          </tbody>
        </table>
        <p v-else class="detail-status">No changes have been recorded for this exhibit.</p>
      </details>

      <!-- File metadata -->
      <details class="detail-section">
        <summary>
          <h3>Metadata</h3>
        </summary>
        <dl class="detail-grid">
          <div>
            <dt>Storage</dt>
            <dd>{{ file.storageProvider }}</dd>
          </div>
          <div>
            <dt>Content type</dt>
            <dd>{{ file.contentType }}</dd>
          </div>
          <div>
            <dt>File size</dt>
            <dd>{{ formatFileSize(file.fileSize) }}</dd>
          </div>
          <div>
            <dt>Submitted</dt>
            <dd>{{ result.submissionDate ? formatDateTime(result.submissionDate, true) : '—' }}</dd>
          </div>
        </dl>
      </details>
    </div>
  </div>
</template>
