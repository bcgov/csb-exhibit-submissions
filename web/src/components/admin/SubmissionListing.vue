
<script setup lang="ts">
import { formatDateTime, splitDateTimeForDisplay } from '@/helpers/formatters';
import type { SubmissionReviewModel } from '@/models/SubmissionReviewModel';
import useSubmissionService from '@/services/SubmissionService';
import type { AxiosError } from 'axios';
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

const { retrieveSubmissionListing } = useSubmissionService();
const router = useRouter();

const submissions = ref<SubmissionReviewModel[] | undefined>(undefined);
const errorMessage = ref<string | null>(null);

onMounted(async () => {
  try {
    submissions.value = await retrieveSubmissionListing();
  } catch (err: unknown) {
    if ((err as AxiosError).isAxiosError) {
      const error = err as AxiosError<unknown>;
      if (error?.response?.status === 403) {
        errorMessage.value = 'You do not have permission to view this data.';
      } else {
        throw error;
      }
    }
  }
});

const selectedId = ref<number | null>(null);

const selectRow = (id: number) => {
  selectedId.value = id;
};

const openReview = (id: number) => {
  router.push(`/admin/view/${id}`);
};

// Returns display string for the File # column, showing "+N more" when multiple tickets.
const fileNumberDisplay = (item: SubmissionReviewModel): string => {
  if (!item.tickets || item.tickets.length === 0) return '';
  const first = item.tickets[0]!.fileNumberText;
  const extra = item.tickets.length - 1;
  return extra > 0 ? `${first} (+${extra} more)` : first;
};

// Returns display string for the Accused name column.
const accusedDisplay = (item: SubmissionReviewModel): string => {
  if (!item.tickets || item.tickets.length === 0) return '';
  const first = item.tickets[0]!.accusedName ?? '';
  const extra = item.tickets.length - 1;
  return extra > 0 ? `${first} (+${extra} more)` : first;
};

// Future pagination
const page = ref(1);
const pageSize = 10;

const totalPages = computed(() =>
  submissions.value ? Math.ceil(submissions.value.length / pageSize) : 0
);

const nextPage = async () => {
  submissions.value = await retrieveSubmissionListing();
};

const prevPage = () => {
  if (page.value > 1) page.value--;
};
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
  background-color: #cac8c8;
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
    <div v-if="errorMessage" class="alert alert-danger">
      {{ errorMessage }}
    </div>
    <table class="submission-table">
      <thead>
        <tr>
          <th>Submission Date</th>
          <th>Court Date</th>
          <th>Time</th>
          <th>Accused name</th>
          <th>File #</th>
          <th>Location</th>
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
          <td>{{ formatDateTime(item.submissionDate ?? '', true) }}</td>
          <td>{{ splitDateTimeForDisplay(item.courtDateTime).date }}</td>
          <td>{{ splitDateTimeForDisplay(item.courtDateTime).time }}</td>
          <td>{{ accusedDisplay(item) }}</td>
          <td :title="item.tickets?.map(t => t.fileNumberText).join(', ')">{{ fileNumberDisplay(item) }}</td>
          <td>{{ item.location }}</td>
          <td>{{ item.room }}</td>
          <td>Pending</td>
        </tr>
      </tbody>
    </table>

    <div class="pagination" v-show="false">
      <button @click="prevPage" :disabled="page === 1">Previous</button>
      <span>Page {{ page }} of {{ totalPages }}</span>
      <button @click="nextPage" :disabled="page === totalPages">Next</button>
    </div>
  </div>
</template>
