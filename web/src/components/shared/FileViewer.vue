<script setup lang="ts">
import { computed } from 'vue';

const props = defineProps({
  fileUrl: {
    type: String,
    required: true,
  },
  downloadUrl: {
    type: String,
    default: '',
  },
  mimeType: {
    type: String,
    default: '',
  },
  hideDownload: {
    type: Boolean,
    default: false,
  },
});

const isVideo = computed(() => props.mimeType.startsWith('video'));
const isImage = computed(() => props.mimeType.startsWith('image'));
const isPdf = computed(() => props.mimeType.includes('pdf'));
const isAudio = computed(() => props.mimeType.startsWith('audio'));
</script>

<template>
  <div class="evidence-viewer" v-if="fileUrl">
    <!-- VIDEO -->
    <video v-if="isVideo" controls class="viewer" preload="metadata">
      <source :src="fileUrl" :type="mimeType" />
      Your browser does not support video playback.
    </video>

    <!-- IMAGE -->
    <img v-else-if="isImage" :src="fileUrl" class="viewer" />

    <!-- PDF -->
    <iframe v-else-if="isPdf" :src="fileUrl" class="viewer"></iframe>

    <!-- AUDIO -->
    <audio v-else-if="isAudio" controls class="audio-viewer">
      <source :src="fileUrl" :type="mimeType" />
      Your browser does not support audio playback.
    </audio>

    <!-- UNKNOWN FILE -->
    <div v-else class="download-only">Preview not available</div>

    <!-- ACTION BAR (hidden when hideDownload is true) -->
    <div v-if="!hideDownload && downloadUrl" class="actions">
      <a :href="downloadUrl" download class="download-btn"> Download File </a>
    </div>
  </div>
</template>
