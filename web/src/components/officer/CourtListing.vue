<template>
  <div class="search-container">
    <h2>Court Search</h2>

    <form @submit.prevent="onSubmit" class="search-form">

      <div class="form-group">
        <label for="appearanceDate">Appearance Date <span class="required">*</span></label>
        <input id="appearanceDate" type="date" v-model="appearanceDate" required />
      </div>

      <div class="form-group autocomplete-wrapper">
        <AutocompleteSelect v-model="selectedLocation" id="locationSearch" label="Location" :items="locations"
          :loading="isLoadingLocations" :disabled="isLoadingLocations" :error="isLocationError"
          :errorText="locationErrorText" placeholder="Start typing to search locations..." required
          :getLabel="(loc: LocationInfo) => loc.name" :getKey="(loc: LocationInfo) => loc.code" :filterFn="(loc: LocationInfo, q: string) =>
            loc.name.toLowerCase().includes(q) ||
            loc.code.toLowerCase().includes(q)
            " />
      </div>

      <div class="form-group">
        <label for="roomSelect">Room <span class="required">*</span></label>
        <select id="roomSelect" v-model="selectedRoom" :disabled="!selectedLocation || availableRooms.length === 0"
          required>
          <option :value="null" disabled>
            {{ selectedLocation ? 'Select a room' : 'Select a location first' }}
          </option>
          <option v-for="room in availableRooms" :key="room.code" :value="room">
            {{ room.code }}
          </option>
        </select>
      </div>

      <div class="form-actions">
        <button type="submit" :disabled="isSubmitDisabled" class="submit-btn">
          <span v-if="isSearching" class="spinner-border spinner-border-sm me-2"></span>
          Search
        </button>
      </div>

    </form>
  </div>
  <div v-if="hasSearched" class="card shadow-sm">
    <div class="card-body p-0">
      <div v-if="searchResults.length > 0" class="table-responsive">
        <table class="table table-hover mb-0 submission-table">
          <thead class="table-light">
            <tr>
              <th>Order</th>
              <th>Time</th>
              <th>Ticket Number</th>
              <th>Accused Name</th>
              <th>Appearance Reason</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="file in searchResults" :key="file.appearanceID" @click="singleClickSelect(file)"
              @dblclick="selectFile(file)"
              :class="{ selected: selectionStore.selectedFile?.appearanceID === file.appearanceID }">
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
                      {{appearance.countPrintSequenceNumber}}: {{ appearance.statuteDescription }}
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
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import useLocationService from '@/services/LocationService';
import useCourtFileService from '@/services/CourtFileService';
import type { CourtRoomsInfo, LocationInfo } from '@/models/LocationInfo';
import type { CourtFileList } from '@/models/CourtFileList';
import AutocompleteSelect from '../shared/AutocompleteSelect.vue';
import type { AxiosError } from 'axios';
import { formatDateTo24hrTime } from '@/helpers/formatters';
import { useCourtFileSelectionStore } from '@/stores/useCourtFileSelectionStore';
import { useRouter } from 'vue-router';

const { getLocations } = useLocationService();
const { getCourtList } = useCourtFileService();
const router = useRouter()

const selectionStore = useCourtFileSelectionStore();

const appearanceDate = ref<string>(new Date().toISOString().split('T')[0]!);
const selectedLocation = ref<LocationInfo | null>(null);
const selectedRoom = ref<CourtRoomsInfo | null>(null);

const locations = ref<LocationInfo[]>([]);
const isLoadingLocations = ref<boolean>(true);
const isLocationError = ref<boolean>(true);
const locationErrorText = ref<string>("");
const searchResults = ref<CourtFileList[]>([]);
const hasSearched = ref(false);
const isSearching = ref(false);


const availableRooms = computed<CourtRoomsInfo[]>(() => {
  return selectedLocation.value?.courtRooms || [];
});

const isFormValid = computed(() => {
  return !!(appearanceDate.value && selectedLocation.value && selectedRoom.value);
});

const isSubmitDisabled = computed(() => {
  return isLoadingLocations.value || isSearching.value || !appearanceDate.value || !selectedLocation.value || !selectedRoom.value;
});

const fetchLocations = async () => {
  isLoadingLocations.value = true;
  try {
    locations.value = await getLocations();
  } catch (error: unknown) {
    console.error("Failed to load locations API:", error);

    isLocationError.value = true;

    let message = 'Failed to load locations';
    let code: string | number | undefined;

    if ((error as AxiosError).isAxiosError) {
      const axiosError = error as AxiosError<any>;

      message =
        axiosError.response?.data?.message ||
        axiosError.message;

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
  try {
    const agencyId = selectedLocation.value.locationId;
    const roomCode = selectedRoom.value.code;

    searchResults.value = await getCourtList(
      agencyId,
      roomCode,
      appearanceDate.value
    );

    searchResults.value.forEach(s => {
      s.locationId = selectedLocation.value?.locationId ?? "N/A";
      s.locationNameText = selectedLocation.value?.name ?? "N/A";
      s.roomCode = selectedRoom.value?.code ?? "";
      s.roomText = selectedRoom.value?.code ?? "";
    })

    console.log("Results fetched:", searchResults.value, typeof searchResults.value[0]?.appearanceDateTime);
    hasSearched.value = true;

  } catch (error) {
    console.error("Failed to fetch court list:", error);
  } finally {
    isSearching.value = false;
  }
};

const singleClickSelect = (file: CourtFileList) => {

  selectionStore.setSelectedFile(file)
}

const selectFile = (file: CourtFileList) => {
  selectionStore.setSelectedFile(file)

  router.push({
    name: 'OfficerSubmissions'
  })
}

onMounted(() => {
  fetchLocations();
});

onMounted(() => {
});
</script>

<style scoped>
.search-container {
  max-width: 500px;
  margin: 0 auto;
  font-family: sans-serif;
}

.form-group {
  margin-bottom: 1rem;
  display: flex;
  flex-direction: column;
}

.submission-table {
  width: 100%;
  border-collapse: collapse;
  margin-bottom: 1rem;
  margin-top: 2rem;
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

.required {
  color: red;
}

input,
select {
  padding: 0.5rem;
  font-size: 1rem;
  border: 1px solid #ccc;
  border-radius: 4px;
}

.autocomplete-wrapper {
  position: relative;
}

.loading-text {
  font-size: 0.8rem;
  color: #666;
  margin-top: 0.25rem;
}

.submit-btn {
  padding: 0.75rem 1.5rem;
  background-color: #007bff;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 1rem;
}

.submit-btn:disabled {
  background-color: #ccc;
  cursor: not-allowed;
}

.offence-list {
  font-size: 0.7rem;
}
</style>