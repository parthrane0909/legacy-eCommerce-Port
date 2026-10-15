import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
//
// `defineConfig` is imported from `vitest/config` rather than `vite` so that the
// `test` block below is a *typed* property of the config object. Importing it
// from `vite` type-checks the object against `UserConfigExport`, which has no
// `test` member, and fails `vue-tsc -b` with TS2769.
export default defineConfig({
  plugins: [vue()],
  resolve: {
    // Mirrored by `compilerOptions.paths` in tsconfig.app.json. If you change one,
    // change the other, or aliased imports will resolve at build time but not in
    // the type-checker.
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    proxy: {
      // The SPA talks to the .NET 10 API on the same origin in development, so no CORS
      // pre-flight is needed for the browser and cookies are first-party.
      '/api': {
        target: process.env.VITE_API_PROXY ?? 'http://localhost:5099',
        changeOrigin: true,
      },
      '/health': {
        target: process.env.VITE_API_PROXY ?? 'http://localhost:5099',
        changeOrigin: true,
      },
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,

    // e2e/ holds Playwright specs, which Vitest must not try to collect.
    exclude: ['e2e/**', 'node_modules/**', 'dist/**'],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'html', 'lcov'],

      // The assignment sets a 60% bar for "stores and critical components", so the gate is
      // scoped to exactly that: the five Pinia stores and the reusable UI components that carry
      // business or interaction logic. Route-level views and the presentational layout shells are
      // exercised end-to-end by Playwright instead, and the API modules are integration-covered
      // by the backend suite.
      include: [
        'src/stores/**',
        'src/components/catalog/**',
        'src/components/cart/**',
        'src/components/checkout/**',
        'src/components/common/**',
        'src/components/account/**',
        'src/components/admin/**',
        'src/utils/**',
      ],
      thresholds: {
        lines: 60,
        functions: 60,
        branches: 60,
        statements: 60,
      },
    },
  },
})
