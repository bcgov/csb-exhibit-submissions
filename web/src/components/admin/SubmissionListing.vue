<script setup lang="ts">
import { SUBMISSION_LIST_PAGE_SIZE } from '@/constants/submission';
import { formatDateTime, splitDateTimeForDisplay } from '@/helpers/formatters';
import type { PagedResult, SubmissionListFilter, SubmissionReviewModel, SubmissionStatus } from '@/models/SubmissionReviewModel';
import useSubmissionService from '@/services/SubmissionService';
import type { AxiosError } from 'axios';
import { computed, onMounted, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';

const { retrieveSubmissionListing } = useSubmissionService();
const router = useRouter();

const pagedResult = ref<PagedResult<SubmissionReviewModel> | undefined>(undefined);
const errorMessage = ref<string | null>(null);
const loading = ref(false);

const filter = reactive<SubmissionListFilter>({
  submissionDateFrom: '',
  submissionDateTo: '',
  fileNumberText: '',
  accusedName: '',
  status: '',
  page: 1,
  pageSize: SUBMISSION_LIST_PAGE_SIZE,
});

const totalPages = computed(() =>
  pagedResult.value ? Math.ceil(pagedResult.value.totalCount / pagedResult.value.pageSize) : 0,
);

const fetchListing = async () => {
  loading.value = true;
  try {
    pagedResult.value = await retrieveSubmissionListing({ ...filter });
  } catch (err: unknown) {
    if ((err as AxiosError).isAxiosError) {
      const error = err as AxiosError<unknown>;
      if (error?.response?.status === 403) {
        errorMessage.value = 'You do not have permission to view this data.';
      } else {
        throw error;
      }
    }
  } finally {
    loading.value = false;
  }
};

onMounted(fetchListing);

const applyFilter = () => {
  filter.page = 1;
  fetchListing();
};

const clearFilter = () => {
  filter.submissionDateFrom = '';
  filter.submissionDateTo = '';
  filter.fileNumberText = '';
  filter.accusedName = '';
  filter.status = '';
  filter.page = 1;
  fetchListing();
};

const goToPage = (p: number) => {
  filter.page = p;
  fetchListing();
};

const selectedId = ref<number | null>(null);

const selectRow = (id: number) => {
  selectedId.value = id;
};

const openReview = (id: number) => {
  router.push(`/admin/view/${id}`);
};

const fileNumberDisplay = (item: SubmissionReviewModel): string => {
  if (!item.tickets || item.tickets.length === 0) return '';
  const first = item.tickets[0]!.fileNumberText;
  const extra = item.tickets.length - 1;
  return extra > 0 ? `${first} (+${extra} more)` : first;
};

const accusedDisplay = (item: SubmissionReviewModel): string => {
  if (!item.tickets || item.tickets.length === 0) return '';
  const first = item.tickets[0]!.accusedName ?? '';
  return first;
};

const statusChipClass = (status: SubmissionStatus | string): string => {
  switch (status) {
    case 'Accepted': return 'status-chip status-accepted';
    case 'Rejected': return 'status-chip status-rejected';
    default: return 'status-chip status-pending';
  }
};
</script>



<template>
  <div class="submission-list-page">
    <h1>Submission Listings</h1>
    <div v-if="errorMessage" class="alert alert-danger">
      {{ errorMessage }}
    </div>

    <!-- Filter panel -->
    <form class="filter-panel" @submit.prevent="applyFilter">
      <div class="filter-row">
        <label>
          Date from
          <input type="date" v-model="filter.submissionDateFrom" />
        </label>
        <label>
          Date to
          <input type="date" v-model="filter.submissionDateTo" />
        </label>
        <label>
          File #
          <input type="text" v-model="filter.fileNumberText" placeholder="e.g. FILE001" />
        </label>
        <label>
          Accused name
          <input type="text" v-model="filter.accusedName" placeholder="contains…" />
        </label>
        <label>
          Status
          <select v-model="filter.status">
            <option value="">All</option>
            <option value="Pending">Pending</option>
            <option value="Accepted">Accepted</option>
            <option value="Rejected">Rejected</option>
          </select>
        </label>
      </div>
      <div class="filter-actions">
        <button type="submit" class="btn btn--primary btn-apply">Apply</button>
        <button type="button" class="btn btn--secondary btn-clear" @click="clearFilter">Clear</button>
      </div>
    </form>

    <p v-if="loading" class="loading-text">Loading…</p>

    <template v-else-if="pagedResult">
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
            <th>Exhibits</th>
            <th>Status</th>
          </tr>
        </thead>

        <tbody>
          <tr v-for="item in pagedResult.items" :key="item.id"
            :class="{ selected: selectedId === item.id, 'row-rejected': item.status === 'Rejected' }"
            @click="selectRow(item.id)" @dblclick="openReview(item.id)">
            <td>{{ formatDateTime(item.submissionDate ?? '', true) }}</td>
            <td>{{ splitDateTimeForDisplay(item.courtDateTime).date }}</td>
            <td>{{ splitDateTimeForDisplay(item.courtDateTime).time }}</td>
            <td>{{ accusedDisplay(item) }}</td>
            <td :title="item.tickets?.map(t => t.fileNumberText).join(', ')">{{ fileNumberDisplay(item) }}</td>
            <td>{{ item.location }}</td>
            <td>{{ item.room }}</td>
            <td>{{ item.exhibitCount }}</td>
            <td><span :class="statusChipClass(item.status)">{{ item.status }}</span></td>
          </tr>
        </tbody>
      </table>

      <div class="pagination" v-if="totalPages > 1">
        <button class="btn btn--secondary" @click="goToPage(filter.page - 1)" :disabled="filter.page <= 1">Previous</button>
        <span>Page {{ filter.page }} of {{ totalPages }} ({{ pagedResult.totalCount }} total)</span>
        <button class="btn btn--secondary" @click="goToPage(filter.page + 1)" :disabled="filter.page >= totalPages">Next</button>
      </div>
    </template>
  </div>
</template>
