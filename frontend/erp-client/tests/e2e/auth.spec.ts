import { expect, test } from '@playwright/test';

/**
 * Smoke E2E — مسیر محافظت‌شده بدون نشست باید به صفحه ورود هدایت شود.
 * اجرا: npx playwright install chromium && npm run test:e2e
 */
test('unauthenticated user is redirected from a protected route to the login page', async ({
  page,
}) => {
  await page.goto('/dashboard');
  await expect(page).toHaveURL(/\/login/);
});

test('login page renders the authentication form', async ({ page }) => {
  await page.goto('/login');
  await expect(page.getByRole('heading')).toBeVisible();
});
