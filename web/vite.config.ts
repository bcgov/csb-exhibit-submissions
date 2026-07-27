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
    // Must stay true. `isolate: false` shares one module registry across every
    // test file in a worker, so the first file to import a module wins and later
    // files' `vi.mock` factories silently never apply — apiClient.retry.spec and
    // ExhibitDetailModal.spec both fail that way, and which files collide depends
    // on how the scheduler packs them. Isolation costs ~8s of wall-clock here;
    // pay it.
    isolate: true,
    // The devcontainer reports 16 cores but has ~2GB of usable RAM, so 16 jsdom
    // workers thrash and time out on startup — which silently dropped whole test
    // files from the run. Cap the pool to what memory can actually carry.
    // (Vitest 4 renamed this; it was `poolOptions.forks.maxForks` under v3.)
    maxWorkers: 4,
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json', 'html'],
      include: ['src/**/*.ts', 'src/**/*.vue'],
      exclude: ['src/main.ts', 'src/plugins/**', 'src/assets/**'],
    },
  },
})
