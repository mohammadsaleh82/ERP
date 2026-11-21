import { defineConfig, devices } from '@playwright/test';

/**
 * پیکربندی تست E2E با Playwright.
 * پیش‌فرض: dev server فرانت‌اند روی 3000 و API روی 5001.
 * اجرای واقعی نیازمند نصب مرورگرهاست:
 *   npx playwright install chromium
 *   npm run test:e2e
 */
const baseURL = process.env.E2E_BASE_URL ?? (process.env.E2E_PREVIEW ? 'http://localhost:4173' : 'http://localhost:3000');

export default defineConfig({
  testDir: './tests/e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  use: {
    baseURL,
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: process.env.E2E_NO_SERVER
    ? undefined
    : {
        command: process.env.E2E_PREVIEW ? 'npm run preview -- --port 4173 --strictPort' : 'npm run dev',
        url: baseURL,
        reuseExistingServer: !process.env.CI,
        timeout: 120_000,
      },
});
