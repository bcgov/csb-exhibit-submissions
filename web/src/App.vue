<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
 import { mdiAccountCircle } from '@mdi/js';
import { ref } from 'vue';
import logo from './assets/bc-logo.svg?url'

const selectedTab = ref('/officer/court-list')
</script>

<template>
  <v-theme-provider :theme="'light'">
    <v-app>
      <v-app-bar app>
        <v-app-bar-title class="mr-4">
          <router-link to="/">
            <img class="logo" :src="logo" alt="logo" width="63" />
          </router-link>
        </v-app-bar-title>
        <v-tabs align-tabs="start" v-model="selectedTab">
          <v-tab value="admin-list" to="/admin/list">Admin Listing</v-tab>
          <v-tab value="court-list" to="/officer/court-list">Court list</v-tab>
          
          <v-spacer></v-spacer>
          <div class="d-flex align-center">
            <v-btn
              spaced="end"
              size="x-large"
              class="text-subtitle-1"
            >
              <span class="text-left">
                <div class="mb-1">User name goes here</div>
              </span>
              <template #append>
                <v-icon :icon="mdiAccountCircle" size="32" />
              </template>
            </v-btn>
          </div>
        </v-tabs>
      </v-app-bar>
      
      <v-main>
        <router-view />
      </v-main>
      </v-app>
      </v-theme-provider>

  <!-- <header>

    <div class="wrapper">
      <nav>
        <RouterLink to="/dev">Dev tools</RouterLink>
        <RouterLink to="/officer/court-list">Court CourtListing</RouterLink>
        <RouterLink to="/admin/list">Admin Listing</RouterLink>
      </nav>
    </div>
  </header> -->

  <!-- <RouterView /> -->
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
