<template>
  <div class="review-page">
    <h1>Submission Review</h1>

    <div v-if="submission" class="submission-details">

      <div class="details-grid">
        <div><strong>Date:</strong> {{ submission.date }}</div>
        <div><strong>Location:</strong> {{ submission.location }}</div>
        <div><strong>Room:</strong> {{ submission.room }}</div>
        <div><strong>Ticket #:</strong> {{ submission.ticketNumber }}</div>
        <div><strong>Disputant:</strong> {{ submission.disputantName }}</div>
        <div><strong>Officer #:</strong> {{ submission.officerNumber }}</div>
      </div>

      <h3>Submitted Files</h3>

      <ul class="file-list">
        <li v-for="file in submission.files" :key="file.id">
          {{ file.name }}
          <button @click="viewFile(file)">View</button>
          <button @click="downloadFile(file)">Download</button>
        </li>
      </ul>

      <div class="actions">
        <button class="accept" @click="acceptSubmission">
          Accept & Save
        </button>

        <button class="remove" @click="removeSubmission">
          Remove Submission
        </button>
      </div>

    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'

interface SubmissionFile {
  id: number
  name: string
  url: string
}

interface SubmissionDetail {
  id: number
  date?: string
  location: string
  room: string
  ticketNumber: string
  disputantName: string
  officerNumber: string
  files: SubmissionFile[]
}

const route = useRoute();
const router = useRouter();
const submission = ref<SubmissionDetail | null>(null);

const submissionId = Number(route.params.id);

// --- Mock Load ---
onMounted(() => {
  submission.value = {
    id: submissionId,
    date: new Date().toISOString().split('T')[0],
    location: 'Victoria Courthouse',
    room: 'Room 2',
    ticketNumber: `TK-${1000 + submissionId}`,
    disputantName: `Defendant ${submissionId}`,
    officerNumber: 'OF-1234',
    files: [
      { id: 1, name: 'photo1.jpg', url: '#' },
      { id: 2, name: 'report.pdf', url: '#' }
    ]
  };
})

// --- File Actions (Mock) ---
const viewFile = (file: SubmissionFile) => {
  alert(`Viewing file: ${file.name}`);
}

const downloadFile = (file: SubmissionFile) => {
  alert(`Downloading file: ${file.name}`);
}

// --- Admin Actions ---
const acceptSubmission = () => {
  alert('Submission accepted (mock)');
  router.push('/admin/submissions');
}

const removeSubmission = () => {
  const confirmed = confirm('Are you sure you want to remove this submission?');
  if (confirmed) {
    alert('Submission removed (mock)');
    router.push('/admin/submissions');
  }
}
</script>

<style scoped>
.review-page {
  padding: 2rem;
  max-width: 800px;
  margin: auto;
}

.details-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: 0.75rem;
  margin-bottom: 2rem;
}

.file-list li {
  display: flex;
  gap: 1rem;
  margin-bottom: 0.5rem;
}

.actions {
  margin-top: 2rem;
  display: flex;
  gap: 1rem;
}

.accept {
  background-color: #4caf50;
  color: white;
}

.remove {
  background-color: #e53935;
  color: white;
}

button {
  padding: 0.5rem 1rem;
  cursor: pointer;
}
</style>