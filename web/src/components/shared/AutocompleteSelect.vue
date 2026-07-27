<template>
  <div class="form-group autocomplete-wrapper" ref="wrapperRef">
    <label :for="id"> {{ label }} <span v-if="required" class="required">*</span> </label>

    <input
      ref="inputRef"
      :id="id"
      type="text"
      v-model="searchQuery"
      @input="onInput"
      @focus="openDropdown"
      @keydown="onKeydown"
      :placeholder="placeholder"
      :disabled="disabled"
      autocomplete="off"
      :required="required"
    />

    <ul v-if="showDropdown && filteredItems.length" class="dropdown-list">
      <li
        v-for="(item, index) in filteredItems"
        :key="getKey(item)"
        :ref="(el) => setItemRef(el, index)"
        @click="selectItem(item)"
        :class="['dropdown-item', { active: index === highlightedIndex }]"
      >
        {{ getLabel(item) }}
      </li>
    </ul>

    <span v-if="loading && !error" class="loading-text">Loading...</span>
    <span v-if="error" class="error-text">{{ errorText }}</span>
  </div>
</template>

<script setup lang="ts" generic="T">
import {
  ref,
  computed,
  watch,
  nextTick,
  onMounted,
  onUnmounted,
  type ComponentPublicInstance,
} from 'vue';

// --- Props ---
const props = defineProps<{
  modelValue: T | null;

  items: T[];
  getLabel: (item: T) => string;
  getKey: (item: T) => string | number;
  filterFn?: (item: T, query: string) => boolean;

  loading?: boolean;
  error?: boolean;
  errorText?: string;
  disabled?: boolean;
  label?: string;
  placeholder?: string;
  required?: boolean;
  id?: string;
}>();

// --- Emits ---
const emit = defineEmits<{
  (e: 'update:modelValue', value: T | null): void;
}>();

// --- State ---
const searchQuery = ref('');
const showDropdown = ref(false);
const highlightedIndex = ref(-1);

const wrapperRef = ref<HTMLElement | null>(null);
const inputRef = ref<HTMLInputElement | null>(null);
const itemRefs = ref<HTMLElement[]>([]);

// --- Default filter ---
const defaultFilter = (item: T, query: string) =>
  props.getLabel(item).toLowerCase().includes(query);

// --- Computed ---
const filteredItems = computed(() => {
  if (!searchQuery.value) return props.items;

  const query = searchQuery.value.toLowerCase();

  return props.items.filter((item) =>
    props.filterFn ? props.filterFn(item, query) : defaultFilter(item, query),
  );
});

// --- Sync input with external model ---
watch(
  () => props.modelValue,
  (newVal) => {
    if (!newVal) {
      searchQuery.value = '';
    } else {
      searchQuery.value = props.getLabel(newVal);
    }
  },
  { immediate: true },
);

// --- Reset refs when list changes ---
watch(filteredItems, () => {
  itemRefs.value = [];
  highlightedIndex.value = -1;
});

// --- Scroll highlighted item into view ---
watch(highlightedIndex, async (index) => {
  if (index < 0) return;

  await nextTick();

  const el = itemRefs.value[index];
  if (el) {
    el.scrollIntoView({
      block: 'nearest',
      // behavior: 'smooth' // optional
    });
  }
});

// --- Methods ---
const openDropdown = () => {
  showDropdown.value = true;
};

const onInput = () => {
  emit('update:modelValue', null);
  highlightedIndex.value = -1;
  showDropdown.value = true;
};

const selectItem = (item: T | undefined) => {
  if (!item) return;
  emit('update:modelValue', item);
  searchQuery.value = props.getLabel(item);
  showDropdown.value = false;
  highlightedIndex.value = -1;
};

// --- Track item refs ---
const setItemRef = (el: Element | ComponentPublicInstance | null, index: number) => {
  if (el instanceof HTMLElement) {
    itemRefs.value[index] = el;
  }
};

// --- Keyboard navigation ---
const onKeydown = (e: KeyboardEvent) => {
  if (!showDropdown.value && e.key === 'ArrowDown') {
    showDropdown.value = true;
    return;
  }

  switch (e.key) {
    case 'ArrowDown':
      e.preventDefault();
      highlightedIndex.value = (highlightedIndex.value + 1) % filteredItems.value.length;
      break;

    case 'ArrowUp':
      e.preventDefault();
      highlightedIndex.value =
        highlightedIndex.value <= 0 ? filteredItems.value.length - 1 : highlightedIndex.value - 1;
      break;

    case 'Enter':
      if (highlightedIndex.value >= 0) {
        e.preventDefault();
        selectItem(filteredItems.value[highlightedIndex.value]);
      }
      break;

    case 'Escape':
      showDropdown.value = false;
      highlightedIndex.value = -1;
      break;
  }
};

// --- Click outside ---
const handleClickOutside = (e: MouseEvent) => {
  if (!wrapperRef.value) return;

  if (!wrapperRef.value.contains(e.target as Node)) {
    showDropdown.value = false;
    highlightedIndex.value = -1;
  }
};

// --- Lifecycle ---
onMounted(() => {
  document.addEventListener('click', handleClickOutside);
});

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside);
});
</script>
