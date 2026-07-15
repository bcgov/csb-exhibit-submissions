<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router';
import { mdiAccountCircle, mdiLogout } from '@mdi/js';
import { ref } from 'vue';
import logo from './assets/bc-logo.svg?url';
import useAuthService from './services/AuthService';
import { useAuthStore } from './stores/authStore';
import { ROLE_ADMIN, ROLE_USER, ROLE_CLERK } from './constants/roles';

const selectedTab = ref('/officer/court-list');
const { logout } = useAuthService();
const handleLogout = () => {
  console.log('Logging out...');
  logout();
};
const authStore = useAuthStore();
</script>

<template>
  <v-theme-provider :theme="'light'">
    <v-app>
      <v-app-bar app>
        <v-app-bar-title class="mr-4">
          <router-link to="/">
            <img class="logo" :src="logo" alt="logo" width="150" />
          </router-link>
        </v-app-bar-title>
        <v-tabs align-tabs="start" v-model="selectedTab">
          <v-tab
            value="admin-exhibit-search"
            to="/admin/exhibit-search"
            v-if="authStore.hasRole(ROLE_ADMIN)"
            >Exhibit Search</v-tab
          >
          <v-tab value="admin-list" to="/admin/list" v-if="authStore.hasRole(ROLE_CLERK)"
            >Submission Listing</v-tab
          >
          <v-tab value="court-list" to="/officer/court-list" v-if="authStore.hasRole(ROLE_USER)"
            >Court list</v-tab
          >

          <v-spacer></v-spacer>
          <div class="d-flex align-center mr-4">
            <v-menu min-width="200px" rounded>
              <template v-slot:activator="{ props }">
                <v-btn v-bind="props" size="x-large" class="text-subtitle-1" variant="text">
                  <span class="mr-2">{{ authStore.user?.id }}</span>
                  <v-icon :icon="mdiAccountCircle" size="32" />
                </v-btn>
              </template>

              <v-list mt-2>
                <v-list-item @click="handleLogout" prepend-icon="">
                  <template v-slot:prepend>
                    <v-icon :icon="mdiLogout" class="mr-2"></v-icon>
                  </template>
                  <v-list-item-title>Logout</v-list-item-title>
                </v-list-item>
              </v-list>
            </v-menu>
          </div>
        </v-tabs>
      </v-app-bar>

      <v-main>
        <router-view />
      </v-main>
    </v-app>
  </v-theme-provider>
</template>
