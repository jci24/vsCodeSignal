import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vitest/config'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    proxy: {
      '/import': {
        changeOrigin: true,
        target: process.env.VITE_BACKEND_PROXY_TARGET ?? 'http://localhost:5094',
      },
      '/workspaces': {
        changeOrigin: true,
        target: process.env.VITE_BACKEND_PROXY_TARGET ?? 'http://localhost:5094',
      },
    },
  },
  test: {
    css: true,
    environment: 'jsdom',
    globals: true,
    setupFiles: './src/shared/test/setup.ts',
  },
})
