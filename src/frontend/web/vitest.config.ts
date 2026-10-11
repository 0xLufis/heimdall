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
      '~': path.resolve(__dirname, './app'),
      '@': path.resolve(__dirname, './app'),
      '~~': path.resolve(__dirname, '.'),
      '@@': path.resolve(__dirname, '.'),
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./vitest.setup.ts'],
    include: ['tests/**/*.test.ts', '../../../tests/frontend/unit/**/*.test.ts'],
  },
})
