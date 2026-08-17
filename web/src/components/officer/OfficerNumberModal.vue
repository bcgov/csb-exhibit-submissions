<script setup lang="ts">
import { OFFICER_NUMBER_MAX_LENGTH, sanitizeOfficerNumber } from '@/constants/user';
import useUserService from '@/services/UserService';
import { useAuthStore } from '@/stores/authStore';
import type { AxiosError } from 'axios';
import { computed, onMounted, onUnmounted, ref } from 'vue';

/**
 * Collects the officer number CES cannot get from IDIR. Shown on Court Search when the
 * signed-in officer has none stored, and reopened from the Exhibit Upload page to change it.
 *
 * Dismissible by design (Esc, backdrop, Cancel): the number is only needed to upload, so an
 * officer browsing the court list must never be trapped here.
 */
const props = defineProps<{
  /** Prefills the input when the officer is editing an existing number. */
  initialValue?: string | null;
}>();

const emit = defineEmits<{
  close: [];
  saved: [officerNumber: string];
}>();

const { saveOfficerNumber } = useUserService();
const authStore = useAuthStore();

const officerNumber = ref(props.initialValue ?? '');
const saving = ref(false);
const errorMessage = ref('');
const inputRef = ref<HTMLInputElement | null>(null);

const canSave = computed(() => officerNumber.value.length > 0 && !saving.value);

// Sanitized as it is typed, so an invalid officer number cannot be entered at all and the
// API's rejection stays a backstop rather than the primary feedback path.
const onInput = (event: Event) => {
  officerNumber.value = sanitizeOfficerNumber((event.target as HTMLInputElement).value);
  // Vue skips the DOM update when the model value is unchanged (a rejected keystroke), so
  // the stripped character would linger in the field without this.
  (event.target as HTMLInputElement).value = officerNumber.value;
};

const onKeydown = (event: KeyboardEvent) => {
  if (event.key === 'Escape' && !saving.value) {
    emit('close');
  }
};

const save = async () => {
  if (!canSave.value) return;

  saving.value = true;
  errorMessage.value = '';

  try {
    const profile = await saveOfficerNumber(officerNumber.value);
    // The store is the single source the submission form reads from.
    authStore.setOfficerNumber(profile.officerNumber);
    emit('saved', profile.officerNumber ?? officerNumber.value);
    emit('close');
  } catch (error: unknown) {
    console.error('Failed to save the officer number', error);
    const axiosError = error as AxiosError<{ message?: string }>;
    // The API's own validation message is the useful one — it says which rule failed.
    errorMessage.value = axiosError.isAxiosError
      ? (axiosError.response?.data?.message ??
        'Could not save your officer number. Please try again.')
      : 'Could not save your officer number. Please try again.';
  } finally {
    saving.value = false;
  }
};

onMounted(() => {
  inputRef.value?.focus();
  document.addEventListener('keydown', onKeydown);
});

onUnmounted(() => {
  document.removeEventListener('keydown', onKeydown);
});
</script>

<template>
  <div class="officer-number-overlay" @click.self="!saving && emit('close')">
    <div
      class="officer-number-dialog"
      role="dialog"
      aria-modal="true"
      aria-labelledby="officer-number-title"
    >
      <h2 id="officer-number-title">Officer Number</h2>

      <p class="officer-number-intro">
        Your officer number is required on every exhibit submission and cannot be read from your
        IDIR account. Enter it once and it will be saved to your profile and filled in for you on
        future submissions.
      </p>

      <form @submit.prevent="save">
        <div class="form-group">
          <label for="officerNumberInput">Officer Number <span class="required">*</span></label>
          <input
            id="officerNumberInput"
            ref="inputRef"
            type="text"
            :value="officerNumber"
            :maxlength="OFFICER_NUMBER_MAX_LENGTH"
            :disabled="saving"
            autocomplete="off"
            aria-describedby="officerNumberHint"
            @input="onInput"
          />
          <small id="officerNumberHint" class="officer-number-hint">
            Up to {{ OFFICER_NUMBER_MAX_LENGTH }} characters. Letters, numbers, dashes and periods
            only.
          </small>
        </div>

        <p v-if="errorMessage" class="officer-number-error" role="alert">{{ errorMessage }}</p>

        <div class="officer-number-actions">
          <button type="button" class="btn btn--tertiary" :disabled="saving" @click="emit('close')">
            Cancel
          </button>
          <button type="submit" class="btn btn--primary" :disabled="!canSave">
            <span v-if="saving" class="spinner-border spinner-border-sm me-2"></span>
            Save
          </button>
        </div>
      </form>
    </div>
  </div>
</template>
