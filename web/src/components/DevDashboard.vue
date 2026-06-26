<template>
  <div class="dev-dashboard">
    <h1>Development Dashboard</h1>

    <div class="tools-grid">
      <div
        v-for="tool in tools"
        :key="tool.name"
        class="tool-card"
      >
        <h3>{{ tool.name }}</h3>
        <p>{{ tool.description }}</p>

        <button
          v-if="tool.action"
          class="btn btn--primary"
          @click="tool.action"
          :disabled="loading"
        >
          {{ loading ? 'Working...' : tool.buttonLabel }}
        </button>

        <router-link
          v-if="tool.route"
          :to="tool.route"
          class="link"
        >
          Open
        </router-link>
      </div>
    </div>

    <div v-if="error" class="error">
      ❌ {{ error }}
    </div>

    <pre v-if="response" class="response">
{{ response }}
    </pre>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { getHealth } from '@/services/devService'

interface DevTool {
  name: string
  description: string
  buttonLabel?: string
  action?: () => Promise<void>
  route?: string
}

const loading = ref(false)
const response = ref<string | null>(null)
const error = ref<string | null>(null)

const testApi = async () => {
  loading.value = true
  error.value = null
  response.value = null

  try {
    const data: boolean = await getHealth()
    response.value = JSON.stringify(data, null, 2)
  } catch (err: unknown) {
    if (err instanceof Error) {
      error.value = err.message
    } else {
      error.value = 'Unexpected error occurred'
    }
  } finally {
    loading.value = false
  }
}

const tools: DevTool[] = [
  {
    name: 'Test API',
    description: 'Calls the backend health endpoint.',
    buttonLabel: 'Test API',
    action: testApi
  },
  {
    name: 'Router Example',
    description: 'Navigate to test route.',
    route: '/dev/test-page'
  }
]
</script>

<style scoped>
.dev-dashboard {
  padding: 2rem;
  max-width: 900px;
  margin: auto;
}

.tools-grid {
  display: grid;
  gap: 1rem;
  grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
  margin-bottom: 2rem;
}

.tool-card {
  padding: 1rem;
  border: 1px solid #ddd;
  border-radius: 8px;
  background: #242424;
}

button {
  margin-top: 0.5rem;
  padding: 0.5rem 1rem;
  cursor: pointer;
}

.link {
  display: inline-block;
  margin-top: 0.5rem;
  text-decoration: underline;
}

.error {
  color: red;
  margin-bottom: 1rem;
}

.response {
  background: #524f4f;
  padding: 1rem;
  overflow-x: auto;
}
</style>