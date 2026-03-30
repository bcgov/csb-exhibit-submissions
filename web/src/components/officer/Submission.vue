<script setup lang="ts">
import type { ExhibitFormModel } from '@/models/ExhibitFormModel'
import useSubmissionService from '@/services/SubmissionService'
import { ref, reactive, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import FileDropZone from '../shared/FileDropZone.vue'
import { useCourtFileSelectionStore } from '@/stores/useCourtFileSelectionStore'
import { formatDate, formatDateTime, formatDateyyyymmdd } from '@/helpers/formatters'
import type { ExhibitSubmissionModel } from '@/models/ExhibitSubmissionModel'

const route = useRoute()
const router = useRouter()

const { submitExhibits } = useSubmissionService()
const selectionStore = useCourtFileSelectionStore()

const caseId = Number(route.params.id)
const uploading = ref(false)
const errorMessage = ref('')


const selectedFile = computed(() => selectionStore.selectedFile)

// --- Mock Auto Fill ---
const form = reactive<ExhibitFormModel>({
  date: '',
  location: '',
  room: '',
  fileNumberText: '',
  disputantName: '',
  officerNumber: '',
})

onMounted(() => {
  console.log(selectedFile.value?.appearanceDateTime, formatDateyyyymmdd( selectedFile.value?.appearanceDateTime ?? ""));
  // Mock simulated API autofill
  form.date = formatDateyyyymmdd( selectedFile.value?.appearanceDateTime ?? "")
  form.location = selectedFile.value?.locationNameText ?? ""
  form.room = `Room ${selectedFile.value?.roomCode}`
  form.fileNumberText = selectedFile.value?.fileNumberText ?? ""
  form.disputantName = selectedFile.value?.accusedName ?? ""
})

const files = ref<File[]>([])

const handleFilesChanged = (newFiles: File[]) => {
  files.value = newFiles
  console.log("files changed", files.value);
}

const submitForm = async () => {
  uploading.value = true
  errorMessage.value = ''

  var submission: ExhibitSubmissionModel = {accusedDOB: selectedFile.value?.accusedDOB ?? "",
    accusedName: selectedFile.value?.accusedName ?? "",
    appearanceDateTime: selectedFile.value?.appearanceDateTime ?? "",
    appearanceId: selectedFile.value?.appearanceID ?? "",
    courtListType: selectedFile.value?.courtListType ?? "",
    fileNumberText: selectedFile.value?.fileNumberText ?? "",
    locationId: selectedFile.value?.locationId ?? "",
    locationNameText: selectedFile.value?.locationNameText ?? "",
    roomCode: selectedFile.value?.roomCode ?? "",
    roomText: selectedFile.value?.roomText ?? "",
    officerNumber: form.officerNumber ?? ""
  }
  // console.log('Submitting form:', form)
  // console.log('Files:', files.value)
  const success = await submitExhibits(submission, files.value)

  uploading.value = false
  
  console.log('api return:', success)
  if (success) router.push(`/officer/court-list`)
  else errorMessage.value = 'Upload failed. Please try again.'
}
</script>

<style scoped>
.exhibit-page {
  padding: 2rem;
  max-width: 800px;
  margin: auto;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: 1rem;
}

.form-field {
  display: flex;
  flex-direction: column;
}

.form-field input {
  padding: 0.5rem;
}

.dropzone {
  margin-top: 2rem;
  padding: 2rem;
  border: 2px dashed #aaa;
  text-align: center;
  cursor: pointer;
}

.dropzone:hover {
  background: #f9f9f9;
}

.small {
  font-size: 0.8rem;
  color: #666;
}

.file-list {
  margin-top: 1rem;
}

.file-list li {
  display: flex;
  justify-content: space-between;
  margin-bottom: 0.5rem;
}

.actions {
  margin-top: 2rem;
  text-align: right;
}

button {
  padding: 0.5rem 1rem;
}
</style>

<template>
  <div class="exhibit-page">
    <h1>Exhibit Upload</h1>

    <form @submit.prevent="submitForm">
      <div class="form-grid">
        <div class="form-field">
          <label>Date</label>
          <input type="date" v-model="form.date"  disabled="true"/>
        </div>

        <div class="form-field">
          <label>Location</label>
          <input type="text" v-model="form.location"  disabled="true"/>
        </div>

        <div class="form-field">
          <label>Room</label>
          <input type="text" v-model="form.room" disabled="true"/>
        </div>

        <div class="form-field">
          <label>File #</label>
          <input type="text" v-model="form.fileNumberText"  disabled="true"/>
        </div>

        <div class="form-field">
          <label>Disputant Name</label>
          <input type="text" v-model="form.disputantName"  disabled="true" />
        </div>

        <div class="form-field">
          <label>Officer Number</label>
          <input type="text" v-model="form.officerNumber" />
        </div>
      </div>

      <!-- Dropzone -->
      <FileDropZone @filesChanged="handleFilesChanged" />

      <div class="actions">
        <button type="submit">Submit Exhibit</button>
      </div>
    </form>
  </div>
</template>
