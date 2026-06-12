<script setup lang="ts">
import { formatDateTime, formatDateyyyymmdd } from '@/helpers/formatters'
import type { ExhibitSubmissionModel, SubmissionTicketModel } from '@/models/ExhibitSubmissionModel'
import type { PriorSubmissionModel } from '@/models/PriorSubmissionModel'
import useSubmissionService from '@/services/SubmissionService'
import type { SubmissionFile } from '@/models/SubmissionReviewModel'
import { useCourtFileSelectionStore } from '@/stores/useCourtFileSelectionStore'
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import FileDropZone from '../shared/FileDropZone.vue'

const router = useRouter()
const { submitExhibits, getSubmissionsByFileNumber } = useSubmissionService()
const selectionStore = useCourtFileSelectionStore()

const uploading = ref(false)
const errorMessage = ref('')
const uploadProgress = ref<number>(0)
const officerNumber = ref('')

const priorExhibits = ref<Map<string, PriorSubmissionModel[]>>(new Map())
const priorExhibitsError = ref(false)

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

// Flat list of prior files across all queried file numbers, deduplicated by file ID.
// Each entry carries its submission date and the file numbers whose prior submissions contain it.
// Automatically excludes files that only belong to tickets that have been removed.
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

// Return a deduplicated list of file numbers across the current ticket set.
const uniqueFileNumbers = computed(() =>
  [...new Set(tickets.value.map(t => t.fileNumberText))]
)

const loadPriorExhibits = async () => {
  priorExhibitsError.value = false
  const results = new Map<string, PriorSubmissionModel[]>()
  try {
    await Promise.all(
      uniqueFileNumbers.value.map(async (fn) => {
        const data = await getSubmissionsByFileNumber(fn)
        results.set(fn, data)
      })
    )
    priorExhibits.value = results
  } catch {
    priorExhibitsError.value = true
  }
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
    uploadProgress.value = success ? 100 : uploadProgress.value
    if (success) {
      router.push({ name: 'OfficerCourtList' })
    } else if (!errorMessage.value) {
      errorMessage.value = 'Upload failed. Please ensure at least one file is selected.'
    }
  }
}
</script>

<style scoped>
.exhibit-page {
  padding: 2rem;
  max-width: 800px;
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
  background: #f8f8f8;
}

.prior-file-item {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 0.45rem 0.75rem;
  border-bottom: 1px solid #eee;
  font-size: 0.85rem;
}

.prior-file-item:last-child {
  border-bottom: none;
}

.prior-file-name {
  flex: 1;
  color: #333;
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

.prior-empty {
  font-size: 0.82rem;
  color: #888;
  font-style: italic;
}

.prior-error {
  font-size: 0.82rem;
  color: #b00;
}

.officer-field {
  margin-bottom: 1.5rem;
  display: flex;
  flex-direction: column;
  max-width: 250px;
}

.dropzone {
  margin-top: 1rem;
}

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

.back-btn:hover {
  background: #5a6268;
}

.error-text {
  font-size: 0.8rem;
  color: red;
  margin-top: 0.25rem;
}

.upload-progress {
  width: 100%;
  margin-top: 1rem;
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
        <div class="ticket-panel-header">Tickets ({{ tickets.length }})</div>
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

      <!-- Prior exhibits panel (read-only) -->
      <div v-if="uniqueFileNumbers.length > 0" class="prior-exhibits-section">
        <h4>Prior Exhibits</h4>

        <p v-if="priorExhibitsError" class="prior-error">
          Could not load prior exhibit history. You can still proceed with the upload.
        </p>

        <template v-else-if="flatPriorFiles.length > 0">
          <ul class="prior-file-list">
            <li v-for="entry in flatPriorFiles" :key="entry.file.id" class="prior-file-item">
              <span class="prior-file-name">{{ entry.file.originalFileName }}</span>
              <span class="prior-file-date">{{ formatDateTime(entry.submissionDate ?? '', true) }}</span>
              <span class="prior-file-tickets">File #{{ entry.fileNumbers.join(', ') }}</span>
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
      <FileDropZone @filesChanged="handleFilesChanged" />

      <div class="upload-progress">
        <div class="progress" style="height: 20px;">
          <div class="progress-bar progress-bar-striped progress-bar-animated bg-primary" role="progressbar"
            :style="{ width: uploadProgress + '%' }" :aria-valuenow="uploadProgress" aria-valuemin="0"
            aria-valuemax="100">
          </div>
        </div>
      </div>

      <span v-if="errorMessage" class="error-text">{{ errorMessage }}</span>

      <div class="actions">
        <button type="button" class="back-btn" @click="goBack">Back</button>
        <button type="submit" :disabled="uploading">Submit Exhibit</button>
      </div>
    </form>
  </div>
</template>
