<script setup lang="ts">
import type { SubmissionReviewModel, SubmissionFile } from '@/models/SubmissionReviewModel'
import useSubmissionService from '@/services/SubmissionService'
import FileViewer from '../shared/FileViewer.vue'
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'

const route = useRoute()
const router = useRouter()

const submissionId = Number(route.params.id)

const previewFile = ref<SubmissionFile | null>(null)

const { retrieveSubmission } = useSubmissionService()

const submission = ref<SubmissionReviewModel | undefined>(undefined)

onMounted(async () => {
  const data = await retrieveSubmission(submissionId)
  if (!data) return

  submission.value = {
    ...data,
    files: data.files.map((f: SubmissionFile) => ({
      ...f,
      viewUrl: `${import.meta.env.VITE_API_URL}/files/${f.id}/view`,
      downloadUrl: `${import.meta.env.VITE_API_URL}/files/${f.id}/download`
    }))
  }
})

const openPreview = (file: SubmissionFile) => {
  previewFile.value = file
}

const closePreview = () => {
  previewFile.value = null
}

const downloadFile = async (file: SubmissionFile) => {
  const response = await fetch(`/api/files/${file.id}/download`)
  const blob = await response.blob()

  const url = window.URL.createObjectURL(blob)

  const a = document.createElement('a')
  a.href = url
  a.download = file.originalFileName
  document.body.appendChild(a)
  a.click()

  a.remove()
  window.URL.revokeObjectURL(url)
}

const acceptSubmission = async () => {
  if (!confirm('Accept this submission?')) return
  alert('Submission accepted (mock)')
  router.push('/admin/submissions')
}

const removeSubmission = async () => {
  if (!confirm('Reject and delete this submission?')) return
  alert('Submission removed (mock)')
  router.push('/admin/submissions')
}

const fileIcon = (type: string) => {
  if (type.startsWith('image')) return '🖼'
  if (type.startsWith('video')) return '🎬'
  if (type.includes('pdf')) return '📄'
  return '📁'
}
</script>

<template>
  <div class="review-page">
    <h1>Submission Review</h1>

    <div v-if="submission">
      <div class="details-grid">
        <div><strong>Date:</strong> {{ submission.date }}</div>
        <div><strong>Location:</strong> {{ submission.location }}</div>
        <div><strong>Room:</strong> {{ submission.room }}</div>
        <div><strong>Ticket #:</strong> {{ submission.ticketNumber }}</div>
        <div><strong>Disputant:</strong> {{ submission.disputantName }}</div>
        <div><strong>Officer #:</strong> {{ submission.officerNumber }}</div>
      </div>

      <h3>Submitted Evidence</h3>

      <div class="file-grid">
        <div class="file-card" v-for="file in submission.files" :key="file.id">
          <div class="icon">{{ fileIcon(file.contentType) }}</div>

          <div class="name">{{ file.originalFileName }}</div>

          <div class="actions">
            <button @click="openPreview(file)">View</button>

            <button @click="downloadFile(file)">Download</button>
          </div>
        </div>
      </div>

      <div class="actions-main">
        <button class="accept" @click="acceptSubmission">Accept & Save</button>

        <button class="remove" @click="removeSubmission">Reject / Delete</button>
      </div>
    </div>

    <div v-if="previewFile" class="preview-modal">
      <div class="modal-content">

        <button class="close" @click="closePreview">✖</button>

        <FileViewer
          :fileUrl="previewFile.viewUrl"
          :mimeType="previewFile.contentType"
        />

      </div>
    </div>
  </div>
</template>

<style scoped>
.review-page {
  padding: 2rem;
  max-width: 900px;
  margin: auto;
}

.details-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: 10px;
  margin-bottom: 30px;
}

.file-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, 180px);
  gap: 15px;
}

.file-card {
  border: 1px solid #ddd;
  border-radius: 6px;
  padding: 10px;
  background: white;
  text-align: center;
}

.icon {
  font-size: 40px;
  margin-bottom: 5px;
}

.name {
  font-size: 0.9rem;
  margin-bottom: 8px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.actions button {
  margin: 3px;
}

.actions-main {
  margin-top: 30px;
  display: flex;
  gap: 10px;
}

.accept {
  background: #4caf50;
  color: white;
}

.remove {
  background: #e53935;
  color: white;
}

.preview-modal {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
}

.modal-content {
  background: white;
  padding: 20px;
  max-width: 1000px;
  width: 90%;
  max-height: 90vh;
  position: relative;
}

.modal-content img,
.modal-content video,
.modal-content iframe {
  max-width: 100%;
  max-height: 70vh;
}

.close {
  position: absolute;
  top: 5px;
  right: 5px;
}
</style>
