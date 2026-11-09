import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'
import path from 'path'

export default defineConfig({
  plugins: [vue()],
  server: {
    fs: {
      allow: ['../../..'],
    },
  },
  resolve: {
    alias: {
      '@vue/test-utils': path.resolve(__dirname, './node_modules/@vue/test-utils'),
      vue: path.resolve(__dirname, './node_modules/vue'),
      '~': path.resolve(__dirname, './app'),
      '@': path.resolve(__dirname, './app'),
      '~~': path.resolve(__dirname, '.'),
      '@@': path.resolve(__dirname, '.'),
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: [path.resolve(__dirname, './vitest.setup.ts')],
    include: ['tests/**/*.test.ts', '../../../tests/frontend/unit/**/*.test.ts'],
    clearMocks: true,
    restoreMocks: true,
  },
})
