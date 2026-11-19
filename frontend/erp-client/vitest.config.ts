import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import path from 'path';

/**
 * پیکربندی Vitest — جدا از vite.config.ts تا تنظیمات build
 * (manualChunks و غیره) با محیط تست تداخل نکند.
 * تست‌های واحد در tests/unit و تست‌های E2E Playwright در tests/e2e قرار دارند.
 */
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./tests/setup.ts'],
    include: ['tests/unit/**/*.test.{ts,tsx}'],
    globals: false,
    css: false,
    restoreMocks: true,
  },
});
