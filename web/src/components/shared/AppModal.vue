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
        <button class="btn-cancel" @click="emit('cancel')">{{ cancelLabel ?? 'Cancel' }}</button>
        <button
          class="btn-confirm"
          :class="{ danger: confirmDanger }"
          @click="emit('confirm')"
        >{{ confirmLabel ?? 'Confirm' }}</button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-box {
  background: white;
  border-radius: 8px;
  padding: 1.5rem 2rem;
  max-width: 480px;
  width: 90%;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.2);
}

.modal-title {
  margin: 0 0 1rem;
  font-size: 1.15rem;
}

.modal-body {
  margin-bottom: 1.5rem;
  font-size: 0.95rem;
  line-height: 1.5;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
}

.btn-cancel {
  background: #f0f0f0;
  color: #333;
}

.btn-confirm {
  background: #4caf50;
  color: white;
}

.btn-confirm.danger {
  background: #e53935;
}
</style>
