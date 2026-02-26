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
      <div
        class="dropzone"
        @dragover.prevent
        @drop.prevent="handleDrop"
        @click="triggerBrowse"
      >
        <p>Drag & Drop files here or click to browse</p>
        <p class="small">Maximum 10 files</p>

        <input
          type="file"
          ref="fileInput"
          multiple
          hidden
          @change="handleBrowse"
        />
      </div>

      <!-- File List -->
      <ul class="file-list" v-if="files.length">
        <li v-for="(file, index) in files" :key="index">
          {{ file.name }}
          <button type="button" @click="removeFile(index)">Remove</button>
        </li>
      </ul>

      <div class="actions">
        <button type="submit">Submit Exhibit</button>
      </div>
    </form>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRoute } from 'vue-router'

interface ExhibitForm {
  date: string
  location: string
  room: string
  ticketNumber: string
  disputantName: string
  officerNumber: string
}

const route = useRoute()
const caseId = Number(route.params.id)

// --- Mock Auto Fill ---
const form = reactive<ExhibitForm>({
  date: '',
  location: '',
  room: '',
  ticketNumber: '',
  disputantName: '',
  officerNumber: ''
})

onMounted(() => {
  // Mock simulated API autofill
  form.date = new Date().toISOString().split('T')[0] ?? "";
  form.location = 'Victoria Courthouse';
  form.room = `Room ${caseId % 5 || 1}`;
  form.ticketNumber = `TK-${1000 + caseId}`;
  form.disputantName = `Defendant ${caseId}`;
  form.officerNumber = 'OF-1234';
})

// --- File Handling ---
const files = ref<File[]>([])
const fileInput = ref<HTMLInputElement | null>(null)

const MAX_FILES = 10

const addFiles = (newFiles: FileList | null) => {
  if (!newFiles) return

  const fileArray = Array.from(newFiles);

  if (files.value.length + fileArray.length > MAX_FILES) {
    alert('Maximum 10 files allowed');
    return
  }

  files.value.push(...fileArray);
}

const handleBrowse = (event: Event) => {
  const target = event.target as HTMLInputElement;
  addFiles(target.files);
}

const handleDrop = (event: DragEvent) => {
  addFiles(event.dataTransfer?.files ?? null);
}

const triggerBrowse = () => {
  fileInput.value?.click();
}

const removeFile = (index: number) => {
  files.value.splice(index, 1);
}

// --- Submit Stub ---
const submitForm = () => {
  console.log('Submitting form:', form);
  console.log('Files:', files.value);

  alert('Mock submit complete (no API yet)');
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