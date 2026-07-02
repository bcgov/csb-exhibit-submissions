<!-- AI Generated login page since its throw away in the future -->
<template>
  <div class="container mt-5">
    <div class="row justify-content-center">
      <div class="col-md-6 col-lg-4">
        <div class="card shadow-sm">
          <div class="card-body">
            <h3 class="card-title text-center mb-4">System Login</h3>

            <div class="alert alert-secondary text-center small mb-4">
              Local Development Authentication
            </div>

            <form @submit.prevent="handleLogin">
              <div class="mb-3">
                <label for="email" class="form-label">Email address</label>
                <input
                  type="email"
                  id="email"
                  class="form-control"
                  v-model="email"
                  required
                  :disabled="isLoading"
                  autocomplete="username"
                />
              </div>

              <div class="mb-4">
                <label for="password" class="form-label">Password</label>
                <input
                  type="password"
                  id="password"
                  class="form-control"
                  v-model="password"
                  required
                  :disabled="isLoading"
                  autocomplete="current-password"
                />
              </div>

              <div v-if="errorMessage" class="alert alert-danger" role="alert">
                {{ errorMessage }}
              </div>

              <button type="submit" class="btn btn-primary w-100" :disabled="isLoading">
                <span
                  v-if="isLoading"
                  class="spinner-border spinner-border-sm me-2"
                  role="status"
                  aria-hidden="true"
                ></span>
                {{ isLoading ? 'Authenticating...' : 'Sign In' }}
              </button>
            </form>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import useAuthService from '@/services/AuthService';
import axios from 'axios';
import { useAuthStore } from '@/stores/authStore';

const router = useRouter();
const route = useRoute();
const { login } = useAuthService();

// State
const email = ref('');
const password = ref('');
const isLoading = ref(false);
const errorMessage = ref<string | null>(null);

// Actions
const handleLogin = async () => {
  // Reset state before attempt
  isLoading.value = true;
  errorMessage.value = null;

  try {
    // We only care if this succeeds or throws. The service handles the JWT.
    await login(email.value, password.value);

    // Redirect to the originally requested route, or default to home/dashboard
    const redirectPath = route.query.redirect as string;

    //if no redirectPath send to determined base route for role
    if (!redirectPath) {
      const authStore = useAuthStore();
      if (authStore.hasRole('Admin')) await router.push({ name: 'AdminSubmissionList' });
      else if (authStore.hasRole('User')) await router.push({ name: 'OfficerCourtList' });
    } else await router.push(redirectPath);
  } catch (error) {
    // Strictly typed error handling
    if (axios.isAxiosError(error)) {
      // Check if the .NET API returned a specific ProblemDetails response
      if (error.response?.status === 401) {
        errorMessage.value = 'Invalid email or password.';
      } else {
        errorMessage.value = error.response?.data?.title || 'A server error occurred during login.';
      }
    } else {
      // Fallback for network failures or non-Axios errors
      errorMessage.value = 'Unable to connect to the authentication server.';
    }

    // Clear the password field on failure for security
    password.value = '';
  } finally {
    isLoading.value = false;
  }
};
</script>
