<script setup lang="ts">
import type { ExhibitFormModel } from '@/models/ExhibitFormModel'
import useSubmissionService from '@/services/SubmissionService'
import { ref, reactive, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import FileDropZone from '../shared/FileDropZone.vue'

const route = useRoute()
const router = useRouter()
const { submitExhibits } = useSubmissionService()
const caseId = Number(route.params.id)
const uploading = ref(false)
const errorMessage = ref('')

// --- Mock Auto Fill ---
const form = reactive<ExhibitFormModel>({
  date: '',
  location: '',
  room: '',
  ticketNumber: '',
  disputantName: '',
  officerNumber: '',
})

onMounted(() => {
  // Mock simulated API autofill
  form.date = new Date().toISOString().split('T')[0] ?? ''
  form.location = 'Victoria Courthouse'
  form.room = `Room ${caseId % 5 || 1}`
  form.ticketNumber = `TK-${1000 + caseId}`
  form.disputantName = `Defendant ${caseId}`
  form.officerNumber = 'OF-1234'
})

const files = ref<File[]>([])

const handleFilesChanged = (newFiles: File[]) => {
  files.value = newFiles
  console.log("files changed", files.value);
}

const submitForm = async () => {
  uploading.value = true
  errorMessage.value = ''
  console.log('Submitting form:', form)
  console.log('Files:', files.value)
  const success = await submitExhibits(form, files.value)

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
          <input type="date" v-model="form.date" />
        </div>

        <div class="form-field">
          <label>Location</label>
          <input type="text" v-model="form.location" />
        </div>

        <div class="form-field">
          <label>Room</label>
          <input type="text" v-model="form.room" />
        </div>

        <div class="form-field">
          <label>Ticket #</label>
          <input type="text" v-model="form.ticketNumber" />
        </div>

        <div class="form-field">
          <label>Disputant Name</label>
          <input type="text" v-model="form.disputantName" />
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
