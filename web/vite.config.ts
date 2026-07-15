import { fileURLToPath, URL } from 'node:url'

import vue from '@vitejs/plugin-vue'
import vueJsx from '@vitejs/plugin-vue-jsx'
import { defineConfig } from 'vitest/config'
import vueDevTools from 'vite-plugin-vue-devtools'

// https://vite.dev/config/
const isTest = process.env.VITEST === 'true'

export default defineConfig({
  // vue-devtools only serves the dev server; loading it under Vitest just adds
  // transform cost to every test file.
  plugins: [vue(), vueJsx(), ...(isTest ? [] : [vueDevTools()])],
  server: {
    host: '0.0.0.0',
    port: Number(process.env.VITE_PORT || 8080),
    proxy: {
      '^/api': {
        target: process.env.API_URL?.replace(/\/$/, '').replace(/\/api$/, '') || 'http://api:8080',
        changeOrigin: true,
        headers: {
          Connection: 'keep-alive',
          'X-Forwarded-Host': 'localhost',
          'X-Forwarded-Port': '9080',
          'X-Base-Href': process.env.WEB_BASE_HREF || '/',
        },
      },
    },
  },
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    // Building a fresh jsdom per test file dominates the run (the tests themselves
    // are ~1.5s of it). Reusing one environment per worker cuts the wall-clock by
    // roughly 4×. Safe here because no test mutates shared module state that the
    // per-file setup does not already reset (MSW handlers reset in afterEach).
    isolate: false,
    // The devcontainer reports 16 cores but has ~2GB of usable RAM, so 16 jsdom
    // workers thrash and time out on startup — which silently dropped whole test
    // files from the run. Cap the pool to what memory can actually carry.
    poolOptions: {
      forks: { maxForks: 4 },
    },
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json', 'html'],
      include: ['src/**/*.ts', 'src/**/*.vue'],
      exclude: ['src/main.ts', 'src/plugins/**', 'src/assets/**'],
    },
  },
})
