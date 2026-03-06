
<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import type { SubmissionReviewModel, SubmissionFile } from '@/models/SubmissionReviewModel'
import useSubmissionService from '@/services/SubmissionService'


const {retrieveSubmissionListing} = useSubmissionService()
const router = useRouter();



const submissions = ref<SubmissionReviewModel[] | undefined>(undefined
  // Array.from({ length: 28 }).map((_, i) => ({
  //   id: i + 1,
  //   date: new Date().toISOString().split('T')[0],
  //   disputantName: `Defendant ${i + 1}`,
  //   ticketNumber: `TK-${1000 + i}`,
  //   officerNumber: `OF-${100 + i}`,
  //   room: `Room ${(i % 4) + 1}`,
  //   status: 'Pending'
  // }))
)
onMounted(async () => {
  submissions.value = await retrieveSubmissionListing()
})


const selectedId = ref<number | null>(null)

const selectRow = (id: number) => {
  selectedId.value = id;
}

const openReview = (id: number) => {
  router.push(`/admin/view/${id}`);
}

// Pagination
const page = ref(1);
const pageSize = 10;

const totalPages = computed(() =>
  submissions.value ? Math.ceil(submissions.value.length / pageSize) : 0
)

// const paginatedSubmissions = computed(() => {
//   const start = (page.value - 1) * pageSize;
//   return submissions.value.slice(start, start + pageSize);
// })

const nextPage = async () => {
  submissions.value = await retrieveSubmissionListing()
}

const prevPage = () => {
  if (page.value > 1) page.value--;
}
</script>

<style scoped>
.submission-list-page {
  padding: 2rem;
}

.submission-table {
  width: 100%;
  border-collapse: collapse;
  margin-bottom: 1rem;
}

.submission-table th,
.submission-table td {
  border: 1px solid #ddd;
  padding: 0.75rem;
}

.submission-table tr:hover {
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



<template>
  <div class="submission-list-page">
    <h1>Submission Listings</h1>

    <table class="submission-table">
      <thead>
        <tr>
          <th>Date</th>
          <th>Disputant</th>
          <th>Ticket #</th>
          <th>Officer #</th>
          <th>Room</th>
          <th>Status</th>
        </tr>
      </thead>

      <tbody>
        <tr
          v-for="item in submissions"
          :key="item.id"
          :class="{ selected: selectedId === item.id }"
          @click="selectRow(item.id)"
          @dblclick="openReview(item.id)"
        >
          <td>{{ item.date }}</td>
          <td>{{ item.disputantName }}</td>
          <td>{{ item.ticketNumber }}</td>
          <td>{{ item.officerNumber }}</td>
          <td>{{ item.room }}</td>
          <td>Pending</td>
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
