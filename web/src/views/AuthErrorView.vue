<script setup lang="ts">
import { computed } from 'vue';
import { useRoute } from 'vue-router';
import { mdiAlertCircleOutline } from '@mdi/js';
import useAuthService from '@/services/AuthService';

/**
 * Dead-end for a failed sign-in. Errors here are unrecoverable unless the user can
 * restart the flow, so the retry button is the point of the page.
 */
const route = useRoute();
const { loginViaKeycloak } = useAuthService();

const reason = computed(() => (route.query.reason as string | undefined) ?? '');
</script>

<template>
  <v-container class="text-center mt-16">
    <v-icon :icon="mdiAlertCircleOutline" size="64" color="grey" />
    <h1 class="text-h5 mt-4">We couldn't sign you in</h1>
    <p v-if="reason" class="text-body-1 mt-2">{{ reason }}</p>
    <p v-else class="text-body-1 mt-2">The sign-in didn't complete. Please try again.</p>
    <v-btn class="mt-6" color="primary" @click="loginViaKeycloak()">Try signing in again</v-btn>
  </v-container>
</template>
