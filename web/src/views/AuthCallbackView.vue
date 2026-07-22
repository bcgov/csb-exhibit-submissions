<script setup lang="ts">
import { onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import api from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import type { AuthCallbackResponse } from '@/models/AuthModels';

/**
 * Landing page for the redirect URI registered with Keycloak (`/auth/callback`).
 *
 * The CES Keycloak client is confidential, so this page cannot exchange the
 * authorization code itself — it hands the code straight to the API, which holds the
 * client secret. The user should never meaningfully see this screen.
 */
const route = useRoute();
const router = useRouter();
const authStore = useAuthStore();

onMounted(async () => {
  const { code, state, error, error_description: errorDescription } = route.query;

  // Keycloak reports failures by redirecting with ?error=, not with an error status,
  // so a cancelled IDIR login must not present as a generic crash.
  if (error) {
    return router.replace({
      name: 'AuthError',
      query: { reason: (errorDescription ?? error) as string },
    });
  }

  if (!code || !state) {
    return router.replace({ name: 'AuthError' });
  }

  // Drop the single-use code from the URL bar and history before anything awaits.
  await router.replace({ path: '/auth/callback', query: {} });

  try {
    // The encrypted ces.login cookie rides along and carries the PKCE verifier.
    const { data } = await api.post<AuthCallbackResponse>('/auth/callback', { code, state });
    authStore.setToken(data.accessToken);
    await router.replace(data.returnUrl || '/');
  } catch {
    await router.replace({ name: 'AuthError' });
  }
});
</script>

<template>
  <v-container class="text-center mt-16">
    <v-progress-circular indeterminate size="48" color="primary" />
    <p class="text-body-1 mt-4">Signing you in…</p>
  </v-container>
</template>
