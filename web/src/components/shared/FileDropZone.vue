<script setup lang="ts">
import { ref } from 'vue'

const emit = defineEmits<{
  (e: 'filesChanged', files: File[]): void
}>()

const files = ref<File[]>([])
const fileInput = ref<HTMLInputElement | null>(null)

const MAX_FILES = 10

const addFiles = (incoming: FileList | null) => {
  if (!incoming) return

  const newFiles = Array.from(incoming)

  if (files.value.length + newFiles.length > MAX_FILES) {
    alert(`Maximum ${MAX_FILES} files allowed`)
    return
  }

  files.value.push(...newFiles)

  emit('filesChanged', files.value)
}

const handleBrowse = (event: Event) => {
  const target = event.target as HTMLInputElement
  addFiles(target.files)
}

const handleDrop = (event: DragEvent) => {
  addFiles(event.dataTransfer?.files ?? null)
}

const removeFile = (index: number) => {
  files.value.splice(index, 1)
  emit('filesChanged', files.value)
}

const triggerBrowse = () => {
  fileInput.value?.click()
}

const reset = () => {
  files.value = []
  if (fileInput.value) fileInput.value.value = ''
  emit('filesChanged', [])
}

defineExpose({ reset })

const getFileIcon = (file: File) => {
  if (file.type.startsWith('image')) return '🖼️'
  if (file.type.startsWith('video')) return '🎬'
  if (file.type === 'application/pdf') return '📄'
  if (file.type.includes('text')) return '📄'
  return '📁'
}
</script>

<template>
  <div class="dropzone"
       @dragover.prevent
       @drop.prevent="handleDrop"
       @click="triggerBrowse">

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

  <ul class="file-list" v-if="files.length">
    <li v-for="(file, index) in files" :key="index">

      <span class="file-info">
        <span class="icon">{{ getFileIcon(file) }}</span>
        {{ file.name }}
      </span>

      <button type="button" @click="removeFile(index)">
        Remove
      </button>

    </li>
  </ul>
</template>

<style scoped>

.dropzone{
  margin-top:2rem;
  padding:2rem;
  border:2px dashed #aaa;
  text-align:center;
  cursor:pointer;
}

.dropzone:hover{
  background:#f9f9f9;
}

.file-list{
  margin-top:1rem;
}

.file-list li{
  display:flex;
  justify-content:space-between;
  margin-bottom:0.5rem;
}

.file-info{
  display:flex;
  gap:0.5rem;
}

.icon{
  font-size:18px;
}

.small{
  font-size:0.8rem;
  color:#666;
}

</style>