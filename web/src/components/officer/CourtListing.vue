<template>
  <div class="search-container">
    <h1>Court Search</h1>

    <form @submit.prevent="onSubmit" class="search-form">
      <div class="form-group">
        <label for="appearanceDate">Appearance Date <span class="required">*</span></label>
        <input id="appearanceDate" type="date" v-model="appearanceDate" required />
      </div>

      <div class="form-group">
        <AutocompleteSelect
          v-model="selectedLocation"
          id="locationSearch"
          label="Location"
          :items="locations"
          :loading="isLoadingLocations"
          :disabled="isLoadingLocations"
          :error="isLocationError"
          :errorText="locationErrorText"
          placeholder="Start typing to search locations..."
          required
          :getLabel="(loc: LocationInfo) => loc.name"
          :getKey="(loc: LocationInfo) => loc.code"
          :filterFn="
            (loc: LocationInfo, q: string) =>
              loc.name.toLowerCase().includes(q) || loc.code.toLowerCase().includes(q)
          "
        />
      </div>

      <div class="form-group">
        <label for="roomSelect">Room <span class="required">*</span></label>
        <select
          id="roomSelect"
          v-model="selectedRoom"
          :disabled="!selectedLocation || availableRooms.length === 0"
          required
        >
          <option :value="null" disabled>
            {{ selectedLocation ? 'Select a room' : 'Select a location first' }}
          </option>
          <option v-for="room in availableRooms" :key="room.code" :value="room">
            {{ room.code }}
          </option>
        </select>
      </div>

      <div class="form-actions">
        <button type="submit" :disabled="isSubmitDisabled" class="btn btn--primary submit-btn">
          <span v-if="isSearching" class="spinner-border spinner-border-sm me-2"></span>
          Search
        </button>
      </div>
    </form>
  </div>

  <div v-if="hasSearched" class="court-results px-4 pb-4">
    <div class="">
      <button
        class="btn btn--primary submit-btn"
        @click="proceedToUpload"
        :disabled="checkedFiles.length <= 0"
      >
        Upload Exhibit ({{ checkedFiles.length }} selected)
      </button>
    </div>
    <div class="card shadow-sm">
      <div class="card-body p-0">
        <div v-if="searchResults.length > 0" class="table-responsive">
          <table class="table table-hover mb-0 submission-table">
            <thead class="table-light">
              <tr>
                <th class="checkbox-col"></th>
                <th>Order</th>
                <th>Time</th>
                <th>Ticket Number</th>
                <th>Accused Name</th>
                <th>Appearance Reason</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="file in searchResults"
                :key="file.appearanceId"
                @click="onRowClick(file)"
                @dblclick="onRowDblClick(file)"
                :class="{ selected: isChecked(file) }"
              >
                <td class="checkbox-col" @click.stop>
                  <input
                    type="checkbox"
                    :checked="isChecked(file)"
                    :disabled="!isSelectable(file)"
                    :title="
                      !isSelectable(file)
                        ? 'This ticket is from a different location, room, or date.'
                        : undefined
                    "
                    @change="toggleCheck(file)"
                  />
                </td>
                <td>{{ file.appearanceSequenceNumber }}</td>
                <td>{{ formatDateTo24hrTime(file.appearanceDateTime) }}</td>
                <td class="text-monospace">{{ file.fileNumberText }}</td>
                <td class="fw-bold">{{ file.accusedName }}</td>
                <td>
                  <div class="d-flex align-items-start">
                    <span class="badge bg-secondary me-2 align-self-start">
                      {{ file.appearanceReasonCode }}
                    </span>
                    <div>
                      <div
                        v-for="(appearance, index) in file.appearanceDetails"
                        :key="index"
                        class="lh-sm mb-1 offence-list"
                      >
                        {{ appearance.countPrintSequenceNumber }}:
                        {{ appearance.statuteDescription }}
                      </div>
                    </div>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div v-else class="text-center py-5">
          <p class="text-muted mb-0">No court files found for the selected criteria.</p>
        </div>
      </div>
    </div>
  </div>

  <!-- Floating upload bar — visible only when at least one ticket is checked -->
  <div v-if="checkedFiles.length > 0" class="floating-upload-bar">
    <span class="selected-count"
      >{{ checkedFiles.length }} ticket{{ checkedFiles.length === 1 ? '' : 's' }} selected</span
    >
    <button class="btn btn--inverse upload-btn" @click="proceedToUpload">
      Upload Exhibit ({{ checkedFiles.length }} selected)
    </button>
  </div>
</template>

<script setup lang="ts">
import { formatDateTo24hrTime } from '@/helpers/formatters';
import type { CourtFileList } from '@/models/CourtFileList';
import type { CourtRoomsInfo, LocationInfo } from '@/models/LocationInfo';
import useCourtFileService from '@/services/CourtFileService';
import useLocationService from '@/services/LocationService';
import { useCourtFileSelectionStore } from '@/stores/useCourtFileSelectionStore';
import type { AxiosError } from 'axios';
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import AutocompleteSelect from '../shared/AutocompleteSelect.vue';

const { getLocations } = useLocationService();
const { getCourtList } = useCourtFileService();
const router = useRouter();
const selectionStore = useCourtFileSelectionStore();

const appearanceDate = ref<string>(new Date().toISOString().split('T')[0]!);
const selectedLocation = ref<LocationInfo | null>(null);
const selectedRoom = ref<CourtRoomsInfo | null>(null);

const locations = ref<LocationInfo[]>([]);
const isLoadingLocations = ref<boolean>(true);
const isLocationError = ref<boolean>(true);
const locationErrorText = ref<string>('');
const searchResults = ref<CourtFileList[]>([]);
const hasSearched = ref(false);
const isSearching = ref(false);
const checkedFiles = ref<CourtFileList[]>([]);

const availableRooms = computed<CourtRoomsInfo[]>(() => selectedLocation.value?.courtRooms || []);

const isSubmitDisabled = computed(
  () =>
    isLoadingLocations.value ||
    isSearching.value ||
    !appearanceDate.value ||
    !selectedLocation.value ||
    !selectedRoom.value,
);

// A row is selectable only if it shares location, room, and date with the first checked ticket.
const isSelectable = (file: CourtFileList): boolean => {
  if (checkedFiles.value.length === 0) return true;
  const anchor = checkedFiles.value[0]!;
  return (
    file.locationId === anchor.locationId &&
    file.roomCode === anchor.roomCode &&
    file.appearanceDateTime?.split('T')[0] === anchor.appearanceDateTime?.split('T')[0]
  );
};

const isChecked = (file: CourtFileList): boolean =>
  checkedFiles.value.some((f) => f.appearanceId === file.appearanceId);

const toggleCheck = (file: CourtFileList) => {
  if (isChecked(file)) {
    checkedFiles.value = checkedFiles.value.filter((f) => f.appearanceId !== file.appearanceId);
  } else if (isSelectable(file)) {
    checkedFiles.value = [...checkedFiles.value, file];
  }
};

const onRowClick = (file: CourtFileList) => {
  if (!isSelectable(file)) return;
  toggleCheck(file);
};

const onRowDblClick = (file: CourtFileList) => {
  // Double-click: select only this ticket and navigate immediately.
  selectionStore.setSelectedFiles([file]);
  router.push({ name: 'OfficerSubmissions' });
};

const proceedToUpload = () => {
  selectionStore.setSelectedFiles([...checkedFiles.value]);
  router.push({ name: 'OfficerSubmissions' });
};

const fetchLocations = async () => {
  isLoadingLocations.value = true;
  try {
    locations.value = await getLocations();
  } catch (error: unknown) {
    console.error('Failed to load locations API:', error);
    isLocationError.value = true;
    let message = 'Failed to load locations';
    let code: string | number | undefined;
    if ((error as AxiosError).isAxiosError) {
      const axiosError = error as AxiosError<{ message?: string }>;
      message = axiosError.response?.data?.message || axiosError.message;
      code = axiosError.response?.status;
    } else if (error instanceof Error) {
      message = error.message;
    }
    locationErrorText.value = `${code ? `[${code}] ` : ''}${message}`;
  } finally {
    isLoadingLocations.value = false;
  }
};

const onSubmit = async () => {
  if (isSubmitDisabled.value || !selectedLocation.value || !selectedRoom.value) return;
  isSearching.value = true;
  hasSearched.value = false;
  checkedFiles.value = [];
  try {
    const agencyId = selectedLocation.value.locationId;
    const roomCode = selectedRoom.value.code;

    searchResults.value = await getCourtList(agencyId, roomCode, appearanceDate.value);

    searchResults.value.forEach((s) => {
      s.locationId = selectedLocation.value?.locationId ?? 'N/A';
      s.locationNameText = selectedLocation.value?.name ?? 'N/A';
      s.roomCode = selectedRoom.value?.code ?? '';
      s.roomText = selectedRoom.value?.code ?? '';
    });

    hasSearched.value = true;
  } catch (error) {
    console.error('Failed to fetch court list:', error);
  } finally {
    isSearching.value = false;
  }
};

onMounted(() => {
  fetchLocations();
});
</script>
