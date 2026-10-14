/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // Khi dev, gọi /api/... sẽ chuyển sang API .NET ở cổng 5080 (tránh lỗi CORS).
    proxy: {
      '/api': 'http://localhost:5080',
    },
  },
  test: {
    environment: 'node',
  },
});
