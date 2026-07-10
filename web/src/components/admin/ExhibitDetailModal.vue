<script setup lang="ts">
import { EXHIBIT_NOTE_MAX_LENGTH } from '@/constants/submission';
import { formatDateTime, formatFileSize, splitDateTimeForDisplay } from '@/helpers/formatters';
import type { ExhibitNoteModel } from '@/models/ExhibitNoteModel';
import type { ExhibitSearchResultModel } from '@/models/ExhibitSearchResultModel';
import type { ExhibitHistoryEntry } from '@/models/SubmissionReviewModel';
import useSubmissionService from '@/services/SubmissionService';
import { computed, onMounted, ref } from 'vue';

const props = defineProps<{
  result: ExhibitSearchResultModel;
}>();

const emit = defineEmits<{
  close: [];
}>();

const { getFileHistory, getExhibitNotes, addExhibitNote } = useSubmissionService();

const file = computed(() => props.result.file);

// --- Change history ---
const history = ref<ExhibitHistoryEntry[]>([]);
const historyLoading = ref(false);
const historyError = ref(false);

const HISTORY_FIELD_LABELS: Record<string, string> = {
  MarkedValue: 'Marked',
  EnteredValue: 'Entered',
  Description: 'Description',
  EvidenceSourceType: 'Source',
};
const historyFieldLabel = (fieldName: string): string =>
  HISTORY_FIELD_LABELS[fieldName] ?? fieldName;

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
  notesLoading.value = true;
  try {
    history.value = await getFileHistory(file.value.id);
  } catch {
    historyError.value = true;
  } finally {
    historyLoading.value = false;
  }
  try {
    notes.value = await getExhibitNotes(file.value.id);
  } catch {
    notesError.value = true;
  } finally {
    notesLoading.value = false;
  }
});

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
        <button
          type="button"
          class="btn btn--icon btn--tertiary close"
          aria-label="Close exhibit details"
          @click="emit('close')"
        >
          ✖
        </button>
      </header>

      <!-- Submission info -->
      <section class="detail-section">
        <h3>Submission</h3>
        <dl class="detail-grid">
          <div><dt>Submission ID</dt><dd>{{ result.submissionId }}</dd></div>
          <div><dt>Location</dt><dd>{{ result.location || '—' }}</dd></div>
          <div><dt>Room</dt><dd>{{ result.room || '—' }}</dd></div>
          <div><dt>Court date</dt><dd>{{ appearance.date || '—' }}</dd></div>
          <div><dt>Court time</dt><dd>{{ appearance.time || '—' }}</dd></div>
          <div><dt>Accused</dt><dd>{{ result.accusedName || '—' }}</dd></div>
          <div class="detail-grid__wide">
            <dt>File numbers</dt>
            <dd>{{ result.fileNumbers.join(', ') || '—' }}</dd>
          </div>
        </dl>
      </section>

      <!-- Exhibit info -->
      <section class="detail-section">
        <h3>Exhibit</h3>
        <dl class="detail-grid">
          <div><dt>Status</dt><dd>{{ file.status ?? 'Unclassified' }}</dd></div>
          <div><dt>Marked</dt><dd>{{ file.markedValue ?? '—' }}</dd></div>
          <div>
            <dt>Marked at</dt>
            <dd>{{ file.markedAt ? formatDateTime(file.markedAt, true) : '—' }}</dd>
          </div>
          <div><dt>Source</dt><dd>{{ file.evidenceSourceType ?? '—' }}</dd></div>
          <div><dt>Entered</dt><dd>{{ file.enteredValue ?? '—' }}</dd></div>
          <div>
            <dt>Entered at</dt>
            <dd>{{ file.enteredAt ? formatDateTime(file.enteredAt, true) : '—' }}</dd>
          </div>
          <div class="detail-grid__wide">
            <dt>Description</dt>
            <dd>{{ file.description || '—' }}</dd>
          </div>
        </dl>
      </section>

      <!-- File metadata -->
      <section class="detail-section">
        <h3>Metadata</h3>
        <dl class="detail-grid">
          <div><dt>Storage</dt><dd>{{ file.storageProvider }}</dd></div>
          <div><dt>Content type</dt><dd>{{ file.contentType }}</dd></div>
          <div><dt>File size</dt><dd>{{ formatFileSize(file.fileSize) }}</dd></div>
          <div>
            <dt>Submitted</dt>
            <dd>{{ result.submissionDate ? formatDateTime(result.submissionDate, true) : '—' }}</dd>
          </div>
        </dl>
      </section>

      <!-- Change history -->
      <section class="detail-section">
        <h3>Change History</h3>
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
      </section>

      <!-- Registry-only notes -->
      <section class="detail-section notes-section">
        <div class="notes-heading">
          <h3>Notes</h3>
          <span class="registry-badge">Registry use only</span>
        </div>

        <p v-if="notesLoading" class="detail-status">Loading…</p>
        <p v-else-if="notesError" class="detail-status detail-error">Could not load notes.</p>
        <ul v-else-if="notes.length > 0" class="notes-list">
          <li v-for="note in notes" :key="note.id" class="note-item">
            <p class="note-text">{{ note.noteText }}</p>
            <p class="note-meta">
              {{ note.createdBy ?? 'Registry' }} · {{ formatDateTime(note.createdAtUTC, true) }}
            </p>
          </li>
        </ul>
        <p v-else class="detail-status">No notes yet.</p>

        <!-- Add note (append-only; immutable once saved) -->
        <div class="note-add">
          <label for="new-note">Add a note</label>
          <textarea
            id="new-note"
            v-model="newNote"
            :maxlength="EXHIBIT_NOTE_MAX_LENGTH"
            rows="3"
            placeholder="Add a registry note (saved permanently and cannot be edited)…"
          ></textarea>
          <div class="note-add-footer">
            <span class="note-counter" :class="{ over: newNote.length > EXHIBIT_NOTE_MAX_LENGTH }">
              {{ EXHIBIT_NOTE_MAX_LENGTH - newNote.length }} remaining
            </span>
            <button
              type="button"
              class="btn btn--primary btn-save-note"
              :disabled="!canSaveNote"
              @click="saveNote"
            >
              {{ savingNote ? 'Saving…' : 'Save note' }}
            </button>
          </div>
          <p v-if="saveNoteError" class="detail-status detail-error">{{ saveNoteError }}</p>
        </div>
      </section>
    </div>
  </div>
</template>
