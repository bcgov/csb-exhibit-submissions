<script setup lang="ts">
defineProps<{
  title?: string
  confirmLabel?: string
  cancelLabel?: string
  confirmDanger?: boolean
}>();

const emit = defineEmits<{
  confirm: []
  cancel: []
}>();
</script>

<template>
  <div class="modal-overlay" @click.self="emit('cancel')">
    <div class="modal-box" role="dialog" aria-modal="true">
      <h2 v-if="title" class="modal-title">{{ title }}</h2>
      <div class="modal-body">
        <slot />
      </div>
      <div class="modal-footer">
        <button class="btn btn--secondary btn-cancel" @click="emit('cancel')">{{ cancelLabel ?? 'Cancel' }}</button>
        <button
          class="btn btn-confirm"
          :class="confirmDanger ? 'btn--danger' : 'btn--success'"
          @click="emit('confirm')"
        >{{ confirmLabel ?? 'Confirm' }}</button>
      </div>
    </div>
  </div>
</template>

