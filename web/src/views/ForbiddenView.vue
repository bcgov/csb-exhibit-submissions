<script setup lang="ts">
import { mdiLockAlertOutline } from '@mdi/js';
</script>

<template>
  <v-container class="text-center mt-16">
    <v-icon :icon="mdiLockAlertOutline" size="64" color="grey" />
    <h1 class="text-h5 mt-4">You don't have access to this page</h1>
    <p class="text-body-1 mt-2">Contact your administrator if you believe this is a mistake.</p>
  </v-container>
</template>
