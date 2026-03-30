<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
 import { mdiAccountCircle, mdiLogout } from '@mdi/js';
import { ref } from 'vue';
import logo from './assets/bc-logo.svg?url'
import useAuthService from './services/AuthService';
import { useAuthStore } from './stores/authStore';

const selectedTab = ref('/officer/court-list')
const {logout} = useAuthService();
const handleLogout = () => {
  console.log('Logging out...')
  logout()  
}
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
          <v-tab value="admin-list" to="/admin/list">Admin Listing</v-tab>
          <v-tab value="court-list" to="/officer/court-list">Court list</v-tab>
          
          <v-spacer></v-spacer>
          <div class="d-flex align-center mr-4">
            <v-menu min-width="200px" rounded>
              <template v-slot:activator="{ props }">
                <v-btn
                  v-bind="props"
                  size="x-large"
                  class="text-subtitle-1"
                  variant="text"
                >
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

<style scoped>
header {
  line-height: 1.5;
  max-height: 100vh;
}

.logo {
  display: block;
  margin: 0 auto 2rem;
}

nav {
  width: 100%;
  font-size: 12px;
  text-align: center;
  margin-top: 2rem;
}

nav a.router-link-exact-active {
  color: var(--color-text);
}

nav a.router-link-exact-active:hover {
  background-color: transparent;
}

nav a {
  display: inline-block;
  padding: 0 1rem;
  border-left: 1px solid var(--color-border);
}

nav a:first-of-type {
  border: 0;
}

@media (min-width: 1024px) {
  header {
    display: flex;
    place-items: center;
    padding-right: calc(var(--section-gap) / 2);
  }

  .logo {
    margin: 0 2rem 0 0;
  }

  header .wrapper {
    display: flex;
    place-items: flex-start;
    flex-wrap: wrap;
    height: 100%;
    width: 300px;
  }

  nav {
    text-align: left;
    margin-left: -1rem;
    font-size: 1rem;

    padding: 1rem 0;
    margin-top: 1rem;

    display: flex;
    flex-direction: column;
  }
}
</style>
