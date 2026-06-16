<script setup lang="ts">
import { formatDateTime, formatDateyyyymmdd } from '@/helpers/formatters'
import type { ExhibitSubmissionModel, SubmissionTicketModel } from '@/models/ExhibitSubmissionModel'
import type { PriorSubmissionModel } from '@/models/PriorSubmissionModel'
import type { SubmissionFile } from '@/models/SubmissionReviewModel'
import useSubmissionService from '@/services/SubmissionService'
import { useCourtFileSelectionStore } from '@/stores/useCourtFileSelectionStore'
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import FileDropZone from '../shared/FileDropZone.vue'
import FileViewer from '../shared/FileViewer.vue'
import {
  CLASSIFICATION_EDIT_WINDOW_SECONDS,
  DESCRIPTION_MAX_LENGTH,
  ENTERED_MAX,
  ENTERED_MIN,
  MARKED_MAX,
  MARKED_MIN,
  SAVE_INDICATOR_FADE_SECONDS,
  VIEWABLE_CONTENT_TYPE_PREFIXES,
} from '@/constants/classification'

const router = useRouter()
const {
  submitExhibits,
  getSubmissionsByFileNumber,
  markExhibit,
  enterExhibit,
  updateExhibitDescription,
} = useSubmissionService()
const selectionStore = useCourtFileSelectionStore()

const uploading = ref(false)
const errorMessage = ref('')
const successMessage = ref('')
const uploadProgress = ref<number>(0)
const officerNumber = ref('')
const dropZoneRef = ref<InstanceType<typeof FileDropZone> | null>(null)

const priorExhibits = ref<Map<string, PriorSubmissionModel[]>>(new Map())
const priorExhibitsError = ref(false)

// Per-file 10-second edit-window tracking (file ID sets)
const markedWindowActive = reactive<Set<string>>(new Set())
const enteredWindowActive = reactive<Set<string>>(new Set())

// Per-file save indicator: 'success' | { error: string } | null
const saveIndicators = reactive<Record<string, 'success' | string | null>>({})

// Local description drafts (file ID -> current text)
const localDescriptions = reactive<Record<string, string>>({})

// History popup state
const historyDialogOpen = ref(false)
const historyFileNumber = ref('')
const historyResults = ref<PriorSubmissionModel[]>([])
const historyLoading = ref(false)
const historyError = ref(false)

// Preview/view modal (officer view-only, no download)
const previewFile = ref<SubmissionFile | null>(null)

// Tickets managed locally so the officer can remove some before submitting.
const tickets = ref<SubmissionTicketModel[]>([])

const sharedDate = computed(() => {
  const dt = selectionStore.selectedFiles[0]?.appearanceDateTime ?? ''
  return formatDateyyyymmdd(dt)
})
const sharedLocation = computed(() => selectionStore.selectedFiles[0]?.locationNameText ?? '')
const sharedRoom = computed(() => {
  const code = selectionStore.selectedFiles[0]?.roomCode ?? ''
  return code ? `Room ${code}` : ''
})

const files = ref<File[]>([])

const handleFilesChanged = (newFiles: File[]) => {
  files.value = newFiles
}

const updateProgress = (percent: number) => {
  uploadProgress.value = percent
}

const removeTicket = (appearanceId: string) => {
  if (tickets.value.length <= 1) return
  tickets.value = tickets.value.filter(t => t.appearanceId !== appearanceId)
}

// Return a deduplicated list of file numbers across the current ticket set.
const uniqueFileNumbers = computed(() => [...new Set(tickets.value.map(t => t.fileNumberText))])

// Flat list of prior files across all queried file numbers, deduplicated by file ID.
const flatPriorFiles = computed(() => {
  const activeFileNumbers = new Set(uniqueFileNumbers.value)
  const submissionFileNumbers = new Map<number, Set<string>>()
  const fileMap = new Map<string, { file: SubmissionFile; submissionDate?: string; submissionId: number }>()

  for (const [fn, submissions] of priorExhibits.value) {
    if (!activeFileNumbers.has(fn)) continue
    for (const sub of submissions) {
      if (!submissionFileNumbers.has(sub.submissionId)) {
        submissionFileNumbers.set(sub.submissionId, new Set())
      }
      submissionFileNumbers.get(sub.submissionId)!.add(fn)

      for (const f of sub.files) {
        if (f.status === 'Removed') continue
        if (!fileMap.has(f.id)) {
          fileMap.set(f.id, { file: f, submissionDate: sub.submissionDate, submissionId: sub.submissionId })
        }
      }
    }
  }

  return [...fileMap.values()].map(({ file, submissionDate, submissionId }) => ({
    file,
    submissionDate,
    fileNumbers: [...(submissionFileNumbers.get(submissionId) ?? [])],
  }))
})

const goBack = () => {
  selectionStore.clear()
  router.push({ name: 'OfficerCourtList' })
}

const loadPriorExhibits = async () => {
  priorExhibitsError.value = false
  const results = new Map<string, PriorSubmissionModel[]>()
  try {
    await Promise.all(
      uniqueFileNumbers.value.map(async fn => {
        const data = await getSubmissionsByFileNumber(fn)
        results.set(fn, data)
      }),
    )
    priorExhibits.value = results
    // Initialise local description drafts from loaded state
    for (const entry of flatPriorFiles.value) {
      if (!(entry.file.id in localDescriptions)) {
        localDescriptions[entry.file.id] = entry.file.description ?? ''
      }
    }
  } catch {
    priorExhibitsError.value = true
  }
}

const updateFileInStore = (updated: SubmissionFile) => {
  for (const submissions of priorExhibits.value.values()) {
    for (const sub of submissions) {
      const idx = sub.files.findIndex(f => f.id === updated.id)
      if (idx !== -1) {
        sub.files[idx] = { ...sub.files[idx], ...updated }
        return
      }
    }
  }
}

const showSaveSuccess = (fileId: string) => {
  saveIndicators[fileId] = 'success'
  setTimeout(() => {
    if (saveIndicators[fileId] === 'success') saveIndicators[fileId] = null
  }, SAVE_INDICATOR_FADE_SECONDS * 1000)
}

const showSaveError = (fileId: string, message: string) => {
  saveIndicators[fileId] = message
}

const startMarkedWindow = (fileId: string) => {
  markedWindowActive.add(fileId)
  setTimeout(() => markedWindowActive.delete(fileId), CLASSIFICATION_EDIT_WINDOW_SECONDS * 1000)
}

const startEnteredWindow = (fileId: string) => {
  enteredWindowActive.add(fileId)
  setTimeout(() => enteredWindowActive.delete(fileId), CLASSIFICATION_EDIT_WINDOW_SECONDS * 1000)
}

const isMarkedEnabled = (file: SubmissionFile): boolean => {
  if (file.enteredValue != null) return false
  if (file.markedValue == null) return true
  return markedWindowActive.has(file.id)
}

const isEnteredEnabled = (file: SubmissionFile): boolean => {
  if (file.enteredValue == null) return true
  return enteredWindowActive.has(file.id)
}

const isDescriptionEnabled = (file: SubmissionFile): boolean => file.enteredValue == null

const isViewable = (contentType: string): boolean =>
  VIEWABLE_CONTENT_TYPE_PREFIXES.some(prefix => contentType.startsWith(prefix))

const onMarkChange = async (file: SubmissionFile, value: string) => {
  if (!value) return
  try {
    const updated = await markExhibit(file.id, { markedValue: value })
    updateFileInStore(updated)
    startMarkedWindow(file.id)
    showSaveSuccess(file.id)
  } catch (err: unknown) {
    const msg = err instanceof Error ? err.message : 'Failed to mark exhibit.'
    showSaveError(file.id, msg)
  }
}

const onEnterChange = async (file: SubmissionFile, value: string) => {
  if (!value) return
  try {
    const updated = await enterExhibit(file.id, { enteredValue: value })
    updateFileInStore(updated)
    // Clear Marked window immediately — only Entered is correctable within its own window
    markedWindowActive.delete(file.id)
    startEnteredWindow(file.id)
    showSaveSuccess(file.id)
  } catch (err: unknown) {
    const msg = err instanceof Error ? err.message : 'Failed to enter exhibit.'
    showSaveError(file.id, msg)
  }
}

const onDescriptionBlur = async (file: SubmissionFile) => {
  const description = localDescriptions[file.id] ?? ''
  if (description === (file.description ?? '')) return // no change
  try {
    const updated = await updateExhibitDescription(file.id, { description })
    updateFileInStore(updated)
    showSaveSuccess(file.id)
  } catch (err: unknown) {
    const msg = err instanceof Error ? err.message : 'Failed to save description.'
    showSaveError(file.id, msg)
  }
}

const openPreview = (file: SubmissionFile) => { previewFile.value = file }
const closePreview = () => { previewFile.value = null }

const loadHistory = async () => {
  if (!historyFileNumber.value.trim()) return
  historyLoading.value = true
  historyError.value = false
  try {
    historyResults.value = await getSubmissionsByFileNumber(historyFileNumber.value.trim())
  } catch {
    historyError.value = true
  } finally {
    historyLoading.value = false
  }
}

const markedLetters = Array.from({ length: 26 }, (_, i) =>
  String.fromCharCode(MARKED_MIN.charCodeAt(0) + i),
)
const enteredNumbers = Array.from({ length: ENTERED_MAX - ENTERED_MIN + 1 }, (_, i) =>
  String(ENTERED_MIN + i),
)

const statusChipClass = (status?: string) => {
  if (status === 'Entered') return 'chip chip-entered'
  if (status === 'Marked') return 'chip chip-marked'
  return 'chip chip-unclassified'
}

const formatClassificationDate = (iso?: string | null): string => {
  if (!iso) return ''
  return formatDateTime(iso, true)
}

onMounted(async () => {
  if (selectionStore.selectedFiles.length === 0) {
    router.push({ name: 'OfficerCourtList' })
    return
  }

  tickets.value = selectionStore.selectedFiles.map(f => ({
    appearanceId: f.appearanceId,
    appearanceDateTime: f.appearanceDateTime,
    appearanceSequenceNumber: f.appearanceSequenceNumber,
    appearanceReasonCode: f.appearanceReasonCode,
    courtListType: f.courtListType,
    fileNumberText: f.fileNumberText,
    accusedName: f.accusedName,
    accusedDOB: f.accusedDOB,
  }))

  await loadPriorExhibits()
})

const submitForm = async () => {
  uploading.value = true
  errorMessage.value = ''

  const submission: ExhibitSubmissionModel = {
    tickets: tickets.value,
    shortDate: sharedDate.value,
    locationId: selectionStore.selectedFiles[0]?.locationId ?? '',
    locationNameText: selectionStore.selectedFiles[0]?.locationNameText ?? '',
    roomCode: selectionStore.selectedFiles[0]?.roomCode ?? '',
    roomText: selectionStore.selectedFiles[0]?.roomText ?? '',
    officerNumber: officerNumber.value,
  }

  let success = false
  try {
    success = await submitExhibits(submission, files.value, updateProgress)
  } catch (error) {
    console.error('Upload failed', error)
    errorMessage.value = 'Failed to upload exhibit. Please try again.'
  } finally {
    uploading.value = false
    if (success) {
      uploadProgress.value = 0
      files.value = []
      dropZoneRef.value?.reset()
      successMessage.value = 'Exhibit uploaded successfully.'
      await loadPriorExhibits()
    } else if (!errorMessage.value) {
      errorMessage.value = 'Upload failed. Please ensure at least one file is selected.'
    }
  }
}
</script>

<style scoped>
.exhibit-page {
  padding: 2rem;
  max-width: 900px;
  margin: auto;
}

.shared-fields {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
  margin-bottom: 1.5rem;
}

.form-field {
  display: flex;
  flex-direction: column;
}

.form-field input {
  padding: 0.5rem;
}

.ticket-panel {
  border: 1px solid #ddd;
  border-radius: 6px;
  margin-bottom: 1.5rem;
}

.ticket-panel-header {
  padding: 0.6rem 1rem;
  background: #f8f8f8;
  font-weight: 600;
  border-bottom: 1px solid #ddd;
  border-radius: 6px 6px 0 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.history-link {
  font-size: 0.78rem;
  background: #e8f0fe;
  color: #1a56db;
  border: none;
  border-radius: 12px;
  padding: 0.15rem 0.6rem;
  cursor: pointer;
  font-weight: 500;
}

.ticket-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.65rem 1rem;
  border-bottom: 1px solid #eee;
  gap: 1rem;
}

.ticket-row:last-child {
  border-bottom: none;
}

.ticket-info {
  flex: 1;
  font-size: 0.9rem;
}

.ticket-file-num {
  font-weight: 600;
  font-family: monospace;
}

.ticket-detail {
  color: #555;
  font-size: 0.8rem;
}

.remove-btn {
  background: none;
  border: 1px solid #c0392b;
  color: #c0392b;
  border-radius: 4px;
  padding: 0.2rem 0.6rem;
  cursor: pointer;
  font-size: 0.8rem;
  white-space: nowrap;
}

.remove-btn:hover {
  background: #fdecea;
}

/* Prior exhibits section */
.prior-exhibits-section {
  margin-bottom: 1.5rem;
}

.prior-exhibits-section h4 {
  margin-bottom: 0.5rem;
  font-size: 0.95rem;
}

.prior-file-list {
  list-style: none;
  margin: 0;
  padding: 0;
  border: 1px solid #e0e0e0;
  border-radius: 4px;
}

.prior-file-item {
  padding: 0.6rem 0.75rem;
  border-bottom: 1px solid #eee;
  font-size: 0.85rem;
}

.prior-file-item:last-child {
  border-bottom: none;
}

.prior-file-row1 {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  margin-bottom: 0.45rem;
  flex-wrap: wrap;
}

.prior-file-name {
  font-weight: 500;
  color: #333;
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.prior-file-date {
  color: #666;
  white-space: nowrap;
  font-size: 0.8rem;
}

.prior-file-tickets {
  font-size: 0.8rem;
  padding: 0.1rem 0.45rem;
  border-radius: 3px;
  background: #e8f0fe;
  color: #1a56db;
  white-space: nowrap;
}

.chip {
  font-size: 0.72rem;
  padding: 0.1rem 0.5rem;
  border-radius: 10px;
  font-weight: 600;
  white-space: nowrap;
}

.chip-unclassified { background: #f0f0f0; color: #555; }
.chip-marked       { background: #fff3cd; color: #856404; }
.chip-entered      { background: #d1e7dd; color: #0a3622; }

.prior-file-row2 {
  display: flex;
  align-items: flex-start;
  gap: 0.6rem;
  flex-wrap: wrap;
}

.classification-group {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
}

.classification-group label {
  font-size: 0.7rem;
  color: #666;
  font-weight: 500;
}

.classification-group select,
.classification-group input[type='text'] {
  padding: 0.2rem 0.35rem;
  font-size: 0.8rem;
  border: 1px solid #ccc;
  border-radius: 3px;
  background: white;
  min-width: 70px;
}

.classification-group select:disabled,
.classification-group input:disabled {
  background: #f5f5f5;
  color: #999;
  cursor: not-allowed;
}

.description-group {
  flex: 1;
  min-width: 180px;
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
}

.description-group label {
  font-size: 0.7rem;
  color: #666;
  font-weight: 500;
}

.description-group input {
  padding: 0.2rem 0.35rem;
  font-size: 0.8rem;
  border: 1px solid #ccc;
  border-radius: 3px;
  width: 100%;
  box-sizing: border-box;
}

.description-group input:disabled {
  background: #f5f5f5;
  color: #999;
  cursor: not-allowed;
}

.desc-counter {
  font-size: 0.68rem;
  color: #888;
  text-align: right;
}

.desc-counter.over {
  color: #c0392b;
}

.timestamp-text {
  font-size: 0.72rem;
  color: #777;
  white-space: nowrap;
}

.save-indicator {
  font-size: 0.8rem;
  margin-left: auto;
  white-space: nowrap;
  align-self: center;
}

.save-success { color: #28a745; }
.save-error   { color: #c0392b; cursor: help; }

.view-btn {
  background: none;
  border: 1px solid #2c7be5;
  color: #2c7be5;
  border-radius: 3px;
  padding: 0.15rem 0.5rem;
  font-size: 0.75rem;
  cursor: pointer;
  white-space: nowrap;
  align-self: flex-start;
}

.view-btn:hover { background: #e8f0fe; }

.prior-empty {
  font-size: 0.82rem;
  color: #888;
  font-style: italic;
}

.prior-error {
  font-size: 0.82rem;
  color: #b00;
}

/* Officer fields */
.officer-field {
  margin-bottom: 1.5rem;
  display: flex;
  flex-direction: column;
  max-width: 250px;
}

.dropzone { margin-top: 1rem; }

.actions {
  margin-top: 1.5rem;
  display: flex;
  gap: 0.75rem;
  align-items: center;
}

.back-btn {
  padding: 0.5rem 1rem;
  background: #6c757d;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
}

.back-btn:hover { background: #5a6268; }

.success-text { font-size: 0.8rem; color: green; margin-top: 0.25rem; }
.error-text   { font-size: 0.8rem; color: red; margin-top: 0.25rem; }

.upload-progress { width: 100%; margin-top: 1rem; }

/* History popup */
.history-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.history-dialog {
  background: white;
  border-radius: 6px;
  padding: 1.5rem;
  width: 90%;
  max-width: 750px;
  max-height: 80vh;
  overflow-y: auto;
}

.history-dialog h3 {
  margin: 0 0 1rem;
  font-size: 1rem;
}

.history-search {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 1rem;
}

.history-search input {
  flex: 1;
  padding: 0.4rem 0.6rem;
  border: 1px solid #ccc;
  border-radius: 4px;
  font-size: 0.9rem;
}

.history-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.8rem;
}

.history-table th,
.history-table td {
  border: 1px solid #ddd;
  padding: 0.4rem 0.6rem;
  text-align: left;
}

.history-table thead { background: #f5f5f5; }

.dialog-footer {
  margin-top: 1rem;
  text-align: right;
}

/* View modal (officer — no download) */
.preview-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.preview-dialog {
  background: white;
  padding: 20px;
  max-width: 1000px;
  width: 90%;
  max-height: 90vh;
  position: relative;
  overflow-y: auto;
}

.close-btn {
  position: absolute;
  top: 5px;
  right: 5px;
}
</style>

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

      <!-- Ticket list panel -->
      <div class="ticket-panel">
        <div class="ticket-panel-header">
          <span>Tickets ({{ tickets.length }})</span>
          <button type="button" class="history-link" @click="historyDialogOpen = true">
            Exhibit History
          </button>
        </div>
        <div v-for="ticket in tickets" :key="ticket.appearanceId" class="ticket-row">
          <div class="ticket-info">
            <span class="ticket-file-num">{{ ticket.fileNumberText }}</span>
            <span class="ticket-detail"> — {{ ticket.accusedName }}</span>
            <span v-if="ticket.appearanceDateTime" class="ticket-detail">
              &nbsp;@ {{ ticket.appearanceDateTime.split('T')[1]?.slice(0, 5) }}
            </span>
          </div>
          <button v-if="tickets.length > 1" type="button" class="remove-btn" @click="removeTicket(ticket.appearanceId)">
            Remove
          </button>
        </div>
      </div>

      <!-- Prior exhibits panel (editable) -->
      <div v-if="uniqueFileNumbers.length > 0" class="prior-exhibits-section">
        <h4>Prior Exhibits</h4>

        <p v-if="priorExhibitsError" class="prior-error">
          Could not load prior exhibit history. You can still proceed with the upload.
        </p>

        <template v-else-if="flatPriorFiles.length > 0">
          <ul class="prior-file-list">
            <li v-for="entry in flatPriorFiles" :key="entry.file.id" class="prior-file-item">

              <!-- Row 1: name, date, ticket badge, status chip, save indicator, view button -->
              <div class="prior-file-row1">
                <span class="prior-file-name">{{ entry.file.originalFileName }}</span>
                <span class="prior-file-date">{{ formatDateTime(entry.submissionDate ?? '', true) }}</span>
                <span class="prior-file-tickets">File #{{ entry.fileNumbers.join(', ') }}</span>
                <span :class="statusChipClass(entry.file.status)">{{ entry.file.status ?? 'Unclassified' }}</span>

                <!-- Save indicator -->
                <span v-if="saveIndicators[entry.file.id] === 'success'" class="save-indicator save-success" title="Saved">✓</span>
                <span
                  v-else-if="saveIndicators[entry.file.id]"
                  class="save-indicator save-error"
                  :title="saveIndicators[entry.file.id] as string"
                >✕</span>

                <!-- View button (browser-viewable types only; no download) -->
                <button
                  v-if="isViewable(entry.file.contentType)"
                  type="button"
                  class="view-btn"
                  @click="openPreview(entry.file)"
                >View</button>
              </div>

              <!-- Row 2: classification controls -->
              <div class="prior-file-row2">

                <!-- Marked -->
                <div class="classification-group">
                  <label>Marked</label>
                  <select
                    :disabled="!isMarkedEnabled(entry.file)"
                    :value="entry.file.markedValue ?? ''"
                    @change="onMarkChange(entry.file, ($event.target as HTMLSelectElement).value)"
                  >
                    <option value="">—</option>
                    <option v-for="letter in markedLetters" :key="letter" :value="letter">{{ letter }}</option>
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
                    :class="{ over: (localDescriptions[entry.file.id]?.length ?? 0) > DESCRIPTION_MAX_LENGTH }"
                  >
                    {{ DESCRIPTION_MAX_LENGTH - (localDescriptions[entry.file.id]?.length ?? 0) }} remaining
                  </span>
                </div>

              </div>
            </li>
          </ul>
        </template>

        <p v-else class="prior-empty">No previous exhibits for the selected tickets.</p>
      </div>

      <!-- Officer number -->
      <div class="officer-field">
        <label>Officer Number</label>
        <input type="text" v-model="officerNumber" />
      </div>

      <!-- Dropzone -->
      <FileDropZone ref="dropZoneRef" @filesChanged="handleFilesChanged" />

      <div class="upload-progress">
        <div class="progress" style="height: 20px;">
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
        <button type="button" class="back-btn" @click="goBack">Back</button>
        <button type="submit" :disabled="uploading">Submit Exhibit</button>
      </div>
    </form>

    <!-- Officer view-only preview modal (no download offered) -->
    <div v-if="previewFile" class="preview-overlay" @click.self="closePreview">
      <div class="preview-dialog">
        <button type="button" class="close-btn" @click="closePreview">✖</button>
        <FileViewer
          :fileUrl="`/api/files/${previewFile.id}/view`"
          :mimeType="previewFile.contentType"
          :hideDownload="true"
        />
      </div>
    </div>

    <!-- Exhibit History popup -->
    <div v-if="historyDialogOpen" class="history-overlay" @click.self="historyDialogOpen = false">
      <div class="history-dialog">
        <h3>Exhibit History by Ticket Number</h3>
        <div class="history-search">
          <input
            v-model="historyFileNumber"
            placeholder="Enter file/ticket number…"
            @keyup.enter="loadHistory"
          />
          <button type="button" @click="loadHistory" :disabled="historyLoading">Search</button>
        </div>

        <p v-if="historyError" class="prior-error">Could not load history. Please try again.</p>
        <p v-else-if="historyLoading" style="color:#666;font-size:0.85rem;">Loading…</p>
        <template v-else-if="historyResults.length > 0">
          <table class="history-table">
            <thead>
              <tr>
                <th>File Name</th>
                <th>Submission Date</th>
                <th>Status</th>
                <th>Marked</th>
                <th>Marked At</th>
                <th>Entered</th>
                <th>Entered At</th>
                <th>Description</th>
              </tr>
            </thead>
            <tbody>
              <template v-for="sub in historyResults" :key="sub.submissionId">
                <tr v-for="file in sub.files.filter(f => f.status !== 'Removed')" :key="file.id">
                  <td>{{ file.originalFileName }}</td>
                  <td>{{ formatDateTime(sub.submissionDate ?? '', true) }}</td>
                  <td>{{ file.status ?? 'Unclassified' }}</td>
                  <td>{{ file.markedValue ?? '—' }}</td>
                  <td>{{ formatClassificationDate(file.markedAt) || '—' }}</td>
                  <td>{{ file.enteredValue ?? '—' }}</td>
                  <td>{{ formatClassificationDate(file.enteredAt) || '—' }}</td>
                  <td>{{ file.description ?? '—' }}</td>
                </tr>
              </template>
            </tbody>
          </table>
        </template>
        <p v-else-if="historyFileNumber && !historyLoading" class="prior-empty">No exhibits found for this ticket number.</p>

        <div class="dialog-footer">
          <button type="button" @click="historyDialogOpen = false">Close</button>
        </div>
      </div>
    </div>
  </div>
</template>
