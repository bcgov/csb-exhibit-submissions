<template>
  <div class="court-list-page">
    <h1>Court List</h1>

    <table class="court-table">
      <thead>
        <tr>
          <th>Name</th>
          <th>Case Number</th>
          <th>Room</th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="item in paginatedCases"
          :key="item.id"
          :class="{ selected: selectedId === item.id }"
          @click="selectRow(item.id)"
          @dblclick="openCase(item.id)"
        >
          <td>{{ item.name }}</td>
          <td>{{ item.caseNumber }}</td>
          <td>{{ item.roomNumber }}</td>
        </tr>
      </tbody>
    </table>

    <div class="pagination">
      <button @click="prevPage" :disabled="page === 1">
        Previous
      </button>

      <span>Page {{ page }} of {{ totalPages }}</span>

      <button @click="nextPage" :disabled="page === totalPages">
        Next
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'

interface CourtCase {
  id: number
  name: string
  caseNumber: string
  roomNumber: string
}

const router = useRouter()

// --- Mock Data ---
const cases = ref<CourtCase[]>(
  Array.from({ length: 37 }).map((_, i) => ({
    id: i + 1,
    name: `Defendant ${i + 1}`,
    caseNumber: `CN-${1000 + i}`,
    roomNumber: `Room ${((i % 5) + 1)}`
  }))
)

// --- Selection ---
const selectedId = ref<number | null>(null)

const selectRow = (id: number) => {
  selectedId.value = id
}

// --- Pagination ---
const page = ref(1)
const pageSize = 10

const totalPages = computed(() =>
  Math.ceil(cases.value.length / pageSize)
)

const paginatedCases = computed(() => {
  const start = (page.value - 1) * pageSize
  return cases.value.slice(start, start + pageSize)
})

const nextPage = () => {
  if (page.value < totalPages.value) {
    page.value++
  }
}

const prevPage = () => {
  if (page.value > 1) {
    page.value--
  }
}

// --- Navigation ---
const openCase = (id: number) => {
  router.push(`/officer/submission/${id}`)
}
</script>

<style scoped>
.court-list-page {
  padding: 2rem;
}

.court-table {
  width: 100%;
  border-collapse: collapse;
  margin-bottom: 1rem;
}

.court-table th,
.court-table td {
  border: 1px solid #ddd;
  padding: 0.75rem;
}

.court-table tr:hover {
  background-color: #f5f5f5;
  cursor: pointer;
}

.selected {
  background-color: #dceeff;
}

.pagination {
  display: flex;
  justify-content: center;
  gap: 1rem;
}
</style>