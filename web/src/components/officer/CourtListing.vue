<template>
  <div class="search-container">
    <h2>Court Search</h2>

    <form @submit.prevent="onSubmit" class="search-form">
      
      <div class="form-group">
        <label for="appearanceDate">Appearance Date <span class="required">*</span></label>
        <input 
          id="appearanceDate" 
          type="date" 
          v-model="appearanceDate" 
          required 
        />
      </div>

      <div class="form-group autocomplete-wrapper">        
        <AutocompleteSelect
          v-model="selectedLocation"
          id="locationSearch"
          label="Location"
          :items="locations"
          :loading="isLoadingLocations"
          :disabled="isLoadingLocations"
          placeholder="Start typing to search locations..."
          required
          :getLabel="(loc:LocationInfo) => loc.name"
          :getKey="(loc:LocationInfo) => loc.code"
          :filterFn="(loc:LocationInfo, q:string) =>
            loc.name.toLowerCase().includes(q) ||
            loc.code.toLowerCase().includes(q)
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
          <option 
            v-for="room in availableRooms" 
            :key="room.code" 
            :value="room"
          >
            {{ room.name }}
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
              <th>File Number</th>
              <th>Time</th>
              <th>Type</th>
              <th>Accused Name</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="file in searchResults" :key="file.appearanceID">
              <td class="text-monospace">{{ file.fileNumber }}</td>
              <td>{{ file.appearanceTime }}</td>
              <td>
                <span class="badge bg-secondary">{{ file.courtListType }}</span>
              </td>
              <td class="fw-bold">{{ file.accusedName }}</td>
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


// --- Service Instantiation ---
const { getLocations } = useLocationService();
const { getCourtList } = useCourtFileService();

// --- Form State ---
// Initialize date to today in YYYY-MM-DD format
const appearanceDate = ref<string>(new Date().toISOString().split('T')[0]!);
const selectedLocation = ref<LocationInfo | null>(null);
const selectedRoom = ref<CourtRoomsInfo | null>(null);

// --- Data State ---
const locations = ref<LocationInfo[]>([]);
const isLoadingLocations = ref<boolean>(true);
const searchResults = ref<CourtFileList[]>([]);
const hasSearched = ref(false);
const isSearching = ref(false);

// --- Autocomplete State ---
// const locationSearchQuery = ref<string>('');
// const showLocationDropdown = ref<boolean>(false);

// --- Computed Properties ---
// const filteredLocations = computed(() => {
//   if (!locationSearchQuery.value) return locations.value;
  
//   const query = locationSearchQuery.value.toLowerCase();
//   return locations.value.filter(loc => 
//     loc.name.toLowerCase().includes(query) || 
//     loc.code.toLowerCase().includes(query)
//   );
// });

const availableRooms = computed<CourtRoomsInfo[]>(() => {
  return selectedLocation.value?.courtRooms || [];
});

const isFormValid = computed(() => {
  return !!(appearanceDate.value && selectedLocation.value && selectedRoom.value);
});

const isSubmitDisabled = computed(() => {
  return isLoadingLocations.value || isSearching.value || !appearanceDate.value || !selectedLocation.value || !selectedRoom.value;
});

// --- Methods ---
const fetchLocations = async () => {
  isLoadingLocations.value = true;
  try {
    locations.value = await getLocations();
  } catch (error) {
    console.error("Failed to load locations API:", error);
  } finally {
    isLoadingLocations.value = false;
  }
};

const onLocationInput = () => {
  // If the user alters the input after selecting, invalidate the current selection and rooms
  if (selectedLocation.value) {
    selectedLocation.value = null;
    selectedRoom.value = null;
  }
  // showLocationDropdown.value = true;
};

// const selectLocation = (loc: LocationInfo) => {
//   selectedLocation.value = loc;
//   locationSearchQuery.value = loc.name; // Display the name in the input
//   // showLocationDropdown.value = false;
//   selectedRoom.value = null; // Reset the room requirement for the new location
// };

const onSubmit = async () => {
  if (isSubmitDisabled.value || !selectedLocation.value || !selectedRoom.value) return;
  isSearching.value = true;
  hasSearched.value = false;
  try {
    // Assuming agencyIdentifierCd maps to the agencyId parameter. 
    // If the API expects the location 'code' instead, swap it to: selectedLocation.value.code
    const agencyId = selectedLocation.value.locationId; 
    const roomCode = selectedRoom.value.code;

    searchResults.value = await getCourtList(
      agencyId, 
      roomCode, 
      appearanceDate.value
    );
    
    // Handle the results (e.g., emit to parent, pass to a store, or render locally)
    console.log("Results fetched:", searchResults.value);
    hasSearched.value = true;
    
  } catch (error) {
    console.error("Failed to fetch court list:", error);
  } finally {
    isSearching.value = false;
  }
};

// --- Lifecycle Hooks ---
onMounted(() => {
  fetchLocations();
});

// Close dropdown when clicking outside (Basic UX implementation)
onMounted(() => {
  // document.addEventListener('click', (e) => {
  //   const target = e.target as HTMLElement;
  //   if (!target.closest('.autocomplete-wrapper')) {
  //     showLocationDropdown.value = false;
  //   }
  // });
});
</script>

<style scoped>
/* Basic styling to make the custom autocomplete behave like a native dropdown */
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

input, select {
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
</style>