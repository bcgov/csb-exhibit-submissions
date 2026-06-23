<script setup lang="ts">
import {
  CLASSIFICATION_EDIT_WINDOW_SECONDS,
  DESCRIPTION_MAX_LENGTH,
  ENTERED_MAX,
  ENTERED_MIN,
  MARKED_MIN,
  SAVE_INDICATOR_FADE_SECONDS,
  VIEWABLE_CONTENT_TYPE_PREFIXES
} from '@/constants/classification'
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
                <span class="prior-file-tickets">
                  File #{{ entry.fileNumbers.length <= 2 ? entry.fileNumbers.join(', ') : entry.fileNumbers[0] }}<span
                    v-if="entry.fileNumbers.length > 2"
                    class="ticket-overflow"
                    :title="entry.fileNumbers.join(' \n')"> (+{{ entry.fileNumbers.length - 1 }})</span>
                </span>
                <span :class="statusChipClass(entry.file.status)">{{ entry.file.status ?? 'Unclassified' }}</span>

                <!-- Save indicator -->
                <span v-if="saveIndicators[entry.file.id] === 'success'" class="save-indicator save-success"
                  title="Saved">✓</span>
                <span v-else-if="saveIndicators[entry.file.id]" class="save-indicator save-error"
                  :title="saveIndicators[entry.file.id] as string">✕</span>

                <!-- View button (browser-viewable types only; no download) -->
                <div class="view-container">
                  <button v-if="isViewable(entry.file.contentType)" type="button" class="view-btn"
                    @click="openPreview(entry.file)">View</button>
                </div>
              </div>

              <!-- Row 2: classification controls -->
              <div class="prior-file-row2">

                <!-- Marked -->
                <div class="classification-group">
                  <label>Marked</label>
                  <select :disabled="!isMarkedEnabled(entry.file)" :value="entry.file.markedValue ?? ''"
                    @change="onMarkChange(entry.file, ($event.target as HTMLSelectElement).value)">
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
                  <select :disabled="!isEnteredEnabled(entry.file)" :value="entry.file.enteredValue ?? ''"
                    @change="onEnterChange(entry.file, ($event.target as HTMLSelectElement).value)">
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
                  <input type="text" :disabled="!isDescriptionEnabled(entry.file)" :maxlength="DESCRIPTION_MAX_LENGTH"
                    v-model="localDescriptions[entry.file.id]" @blur="onDescriptionBlur(entry.file)"
                    placeholder="Optional description…" />
                  <span class="desc-counter"
                    :class="{ over: (localDescriptions[entry.file.id]?.length ?? 0) > DESCRIPTION_MAX_LENGTH }">
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
          <div class="progress-bar progress-bar-striped progress-bar-animated bg-primary" role="progressbar"
            :style="{ width: uploadProgress + '%' }" :aria-valuenow="uploadProgress" aria-valuemin="0"
            aria-valuemax="100">
          </div>
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
        <FileViewer :fileUrl="`/api/files/${previewFile.id}/view`" :mimeType="previewFile.contentType"
          :hideDownload="true" />
      </div>
    </div>

    <!-- Exhibit History popup -->
    <div v-if="historyDialogOpen" class="history-overlay" @click.self="historyDialogOpen = false">
      <div class="history-dialog">
        <h3>Exhibit History by Ticket Number</h3>
        <div class="history-search">
          <input v-model="historyFileNumber" placeholder="Enter file/ticket number…" @keyup.enter="loadHistory" />
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
        <p v-else-if="historyFileNumber && !historyLoading" class="prior-empty">No exhibits found for this ticket
          number.
        </p>

        <div class="dialog-footer">
          <button type="button" @click="historyDialogOpen = false">Close</button>
        </div>
      </div>
    </div>
  </div>
</template>
