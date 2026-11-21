/**
 * سناریوی ۱ — ورود، انتخاب شرکت، داشبورد، سال مالی، حساب‌ها.
 * Mock قراردادی مطابق backend؛ جریان کاملاً از UI واقعی.
 */
import { expect, test } from '@playwright/test';
import { loginAs, setupMock, collectConsoleErrors, criticalErrors, USER, COMPANIES } from './support/helpers';

test.describe('سناریوی ۱ — ورود تا حساب‌ها', () => {
  test.beforeEach(async ({ page }) => {
    await setupMock(page);
  });

  test('ورود → انتخاب شرکت → داشبورد → سال مالی ۱۴۰۴ → حساب‌ها', async ({ page }) => {
    const errors = collectConsoleErrors(page);

    // ورود + انتخاب شرکت
    await loginAs(page, USER.full, COMPANIES.c1);
    expect(new URL(page.url()).pathname).toBe('/');

    // داشبورد با داده واقعی mock (سند Posted اولیه: صندوق ۵٬۰۰۰٬۰۰۰)
    await expect(page.getByTestId('summary-cards')).toBeVisible();
    await expect(page.getByText('آریا صنعت').first()).toBeVisible();
    await expect(page.getByText(/سال مالی ۱۴۰۴/).first()).toBeVisible();
    await expect(page.getByText('صندوق').first()).toBeVisible(); // TopBalances از /Ledger/balances

    // خطای بحرانی console ممنوع
    expect(criticalErrors(errors), `console errors: ${criticalErrors(errors).join(' | ')}`).toHaveLength(0);
  });

  test('مشاهده حساب‌ها — درخت حساب با ۴ حساب قراردادی', async ({ page }) => {
    await loginAs(page, USER.full, COMPANIES.c1);
    await page.goto('/accounting/accounts');
    await expect(page.getByTestId('accounts-page')).toBeVisible();
    for (const name of ['صندوق', 'حساب‌های دریافتنی', 'درآمد فروش', 'هزینه اداری']) {
      await expect(page.getByText(name).first()).toBeVisible();
    }
  });

  test('کاربر بدون نشست از مسیر محافظت‌شده به login هدایت می‌شود', async ({ page }) => {
    await page.goto('/accounting/documents');
    await expect(page).toHaveURL(/\/login/);
    await expect(page.getByLabel(/ایمیل سازمانی/)).toBeVisible();
  });
});
