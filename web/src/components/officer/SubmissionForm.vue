<script setup lang="ts">
import type { ExhibitFormModel } from '@/models/ExhibitFormModel'
import useSubmissionService from '@/services/SubmissionService'
import { ref, reactive, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import FileDropZone from '../shared/FileDropZone.vue'
import { useCourtFileSelectionStore } from '@/stores/useCourtFileSelectionStore'
import { formatDateyyyymmdd } from '@/helpers/formatters'
import type { ExhibitSubmissionModel } from '@/models/ExhibitSubmissionModel'

const router = useRouter()

const { submitExhibits } = useSubmissionService()
const selectionStore = useCourtFileSelectionStore()

const uploading = ref(false)
const errorMessage = ref('')
const uploadProgress = ref<number>(0);


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
  console.log(selectedFile.value?.appearanceDateTime, formatDateyyyymmdd(selectedFile.value?.appearanceDateTime ?? ""));
  console.log(selectedFile.value);
  form.date = formatDateyyyymmdd(selectedFile.value?.appearanceDateTime ?? "")
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

const updateProgress = (percent: number) => {
  console.log("update progress", percent)
  uploadProgress.value = percent;
};

const submitForm = async () => {
  uploading.value = true
  errorMessage.value = ''

  const submission: ExhibitSubmissionModel = {
    accusedDOB: selectedFile.value?.accusedDOB ?? "",
    accusedName: selectedFile.value?.accusedName ?? "",
    appearanceDateTime: selectedFile.value?.appearanceDateTime ?? "",
    shortDate: formatDateyyyymmdd(selectedFile.value?.appearanceDateTime ?? ""),
    appearanceId: selectedFile.value?.appearanceID ?? "",
    appearanceReasonCode: selectedFile.value?.appearanceReasonCode ?? "",
    appearanceSequenceNumber: selectedFile.value?.appearanceSequenceNumber ?? "",
    courtListType: selectedFile.value?.courtListType ?? "",
    fileNumberText: selectedFile.value?.fileNumberText ?? "",
    locationId: selectedFile.value?.locationId ?? "",
    locationNameText: selectedFile.value?.locationNameText ?? "",
    roomCode: selectedFile.value?.roomCode ?? "",
    roomText: selectedFile.value?.roomText ?? "",
    officerNumber: form.officerNumber ?? ""
  }
  console.log('Submitting form:', submission);
  // console.log('Files:', files.value)
  let success = false
  try {
    success = await submitExhibits(submission, files.value, updateProgress)
  }
  catch (error) {console.error("Upload failed", error);
    errorMessage.value = "Failed to upload exhibit. Please try again.";
  } finally {
    setTimeout(() => {
      uploading.value = false
      uploadProgress.value = 100
      if (success) router.push(`/officer/court-list`)
      else errorMessage.value = 'Upload failed. Please ensure at least one file is selected.'
      uploading.value = false;
    }, 500);
  }
  console.log('api return:', success)
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

.error-text {
  font-size: 0.8rem;
  color: red;
  margin-top: 0.25rem;
}
.upload-progress {
  width: 100%;
}
</style>

<template>
  <div class="exhibit-page">
    <h1>Exhibit Upload</h1>

    <form @submit.prevent="submitForm">
      <div class="form-grid">
        <div class="form-field">
          <label>Date</label>
          <input type="date" v-model="form.date" disabled="true" />
        </div>

        <div class="form-field">
          <label>Location</label>
          <input type="text" v-model="form.location" disabled="true" />
        </div>

        <div class="form-field">
          <label>Room</label>
          <input type="text" v-model="form.room" disabled="true" />
        </div>

        <div class="form-field">
          <label>File #</label>
          <input type="text" v-model="form.fileNumberText" disabled="true" />
        </div>

        <div class="form-field">
          <label>Disputant Name</label>
          <input type="text" v-model="form.disputantName" disabled="true" />
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
      <span v-if="errorMessage" class="error-text">{{ errorMessage }}</span>
    </form>
  </div>
</template>
