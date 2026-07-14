<script setup lang="ts">
import {
  DESCRIPTION_INPUT_MAX_ROWS,
  DESCRIPTION_INPUT_MIN_ROWS,
  DESCRIPTION_MAX_LENGTH,
  DESCRIPTION_PREVIEW_MAX_LENGTH,
} from '@/constants/classification';
import type { SubmissionFile } from '@/models/SubmissionReviewModel';
import { computed, ref } from 'vue';

/**
 * The description slot of an exhibit-list row (CES-42). Description entries are
 * append-only, so this control has exactly two modes:
 *  - the exhibit has no entries → an auto-growing textarea that adds the *first* one;
 *  - the exhibit has entries    → a read-only render of the first entry. Addenda are
 *    added from the exhibit detail modal, never from the list.
 *
 * `compact` is the condensed-row form: one truncated line, no label, no counter.
 */
const props = defineProps<{
  file: SubmissionFile;
  compact?: boolean;
  disabled?: boolean;
  /** Persists the first description. Resolves true on success, so the draft clears. */
  saveFn: (text: string) => Promise<boolean>;
}>();

const draft = ref('');
const saving = ref(false);
const textarea = ref<HTMLTextAreaElement | null>(null);

const descriptions = computed(() => props.file.descriptions ?? []);
const firstDescription = computed(() => descriptions.value[0] ?? null);
const addendumCount = computed(() => Math.max(0, descriptions.value.length - 1));

// Single-line preview: interior newlines/runs of whitespace collapse to single spaces
// so the condensed row stays one line high.
const preview = computed(() => {
  const text = firstDescription.value?.descriptionText ?? '';
  const flat = text.replace(/\s+/g, ' ').trim();
  return flat.length > DESCRIPTION_PREVIEW_MAX_LENGTH
    ? `${flat.slice(0, DESCRIPTION_PREVIEW_MAX_LENGTH)}…`
    : flat;
});

const remaining = computed(() => DESCRIPTION_MAX_LENGTH - draft.value.length);

// Grow the textarea with its content up to DESCRIPTION_INPUT_MAX_ROWS, then scroll.
const autoGrow = () => {
  const el = textarea.value;
  if (!el) return;
  el.style.height = 'auto';
  const lineHeight = parseFloat(getComputedStyle(el).lineHeight) || 0;
  const maxHeight = lineHeight * DESCRIPTION_INPUT_MAX_ROWS;
  el.style.height = `${Math.min(el.scrollHeight, maxHeight)}px`;
  el.style.overflowY = el.scrollHeight > maxHeight ? 'auto' : 'hidden';
};

const onBlur = async () => {
  const text = draft.value.trim();
  if (text.length === 0 || saving.value) return;
  saving.value = true;
  try {
    if (await props.saveFn(text)) draft.value = '';
  } finally {
    saving.value = false;
  }
};
</script>

<template>
  <div class="description-cell" :class="{ 'description-cell--compact': compact }">
    <!-- Has entries: read-only. Correcting one means adding an addendum in the details modal. -->
    <template v-if="firstDescription">
      <label v-if="!compact">Description</label>
      <span
        v-if="compact"
        class="desc-preview"
        :title="firstDescription.descriptionText"
        data-test="desc-preview"
        >{{ preview }}</span
      >
      <p v-else class="desc-full" data-test="desc-full">{{ firstDescription.descriptionText }}</p>
      <span v-if="addendumCount > 0" class="desc-addenda" data-test="desc-addenda">
        +{{ addendumCount }}<template v-if="!compact"> more — open details</template>
      </span>
    </template>

    <!-- No entries: the one place the first description can be added from the list. -->
    <template v-else>
      <label v-if="!compact" :for="`desc-${file.id}`">Description</label>
      <textarea
        :id="`desc-${file.id}`"
        ref="textarea"
        v-model="draft"
        class="desc-input"
        data-test="desc-input"
        :rows="DESCRIPTION_INPUT_MIN_ROWS"
        :maxlength="DESCRIPTION_MAX_LENGTH"
        :disabled="disabled"
        :placeholder="compact ? 'Add a description…' : 'Add a description (saved permanently)…'"
        @input="autoGrow"
        @blur="onBlur"
      ></textarea>
      <span v-if="!compact" class="desc-counter" :class="{ over: remaining < 0 }">
        {{ remaining }} remaining
      </span>
    </template>
  </div>
</template>
