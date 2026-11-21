/**
 * سناریوی ۵ — امنیت دسترسی و جداسازی داده شرکت‌ها:
 * کاربر فاقد مجوز، deny صفحات گزارش، تعویض شرکت بدون نشت داده شرکت قبلی، خروج با پاک‌سازی cache.
 */
import { expect, test } from '@playwright/test';
import { loginAs, setupMock, USER, COMPANIES } from './support/helpers';

test.describe('سناریوی ۵ — مجوز و تعویض شرکت', () => {
  test.beforeEach(async ({ page }) => {
    await setupMock(page);
  });

  test('کاربر محدود: منوی گزارش‌ها مخفی، دسترسی مستقیم به AccessDenied می‌رود', async ({ page }) => {
    await loginAs(page, USER.limited, COMPANIES.c1);
    // منوی سایدبار بدون آیتم‌های گزارش
    await expect(page.getByRole('link', { name: /ترازنامه/ })).not.toBeVisible();
    await expect(page.getByRole('link', { name: /دفتر روزنامه/ })).not.toBeVisible();
    // دسترسی مستقیم با URL → صفحه دسترسی‌نامجاز (PermissionGuard inline رندر می‌کند)
    await page.goto('/accounting/reports/trial-balance');
    await expect(page.getByText(/دسترسی|مجاز/).first()).toBeVisible({ timeout: 10_000 });
    await expect(page.getByTestId('voucher-form')).not.toBeVisible();
  });

  test('تعویض شرکت: داده شرکت جدید می‌آید و داده شرکت قبلی نمایش داده نمی‌شود', async ({ page }) => {
    await loginAs(page, USER.full, COMPANIES.c1);
    // داده شرکت آریا: صندوق ۵٬۰۰۰٬۰۰۰
    await expect(page.getByText('۵٬۰۰۰٬۰۰۰').first()).toBeVisible({ timeout: 10_000 });

    // تعویض به بام پارس از Header
    await page.getByRole('button', { name: 'انتخاب شرکت فعال' }).click();
    await page.getByRole('button', { name: /بام پارس/ }).click();
    await page.waitForURL((url) => url.pathname === '/');
    await expect(page.getByTestId('dashboard-page')).toBeVisible();

    // داده بام: ۹۰۰٬۰۰۰ — و اثر داده آریا نباید باشد
    await expect(page.getByText('۹۰۰٬۰۰۰').first()).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('۵٬۰۰۰٬۰۰۰')).toHaveCount(0);

    // اسناد شرکت بام: فقط سند شماره ۱، بدون سند ۱۰۰ آریا
    await page.goto('/accounting/documents');
    await expect(page.getByTestId('vouchers-page')).toBeVisible();
    await expect(page.getByText(/سال مالی ۱۴۰۳/).first()).toBeVisible(); // بج سال مالی هدر
    const rows = page.locator('table tbody tr');
    const bodyText = await page.locator('table tbody').textContent();
    expect(bodyText).not.toContain('فروش نقدی فروردین'); // سند شرکت آریا
    expect(await rows.count()).toBeLessThanOrEqual(5);
  });

  test('بازگشت به شرکت اول: داده آریا برمی‌گردد (بدون مخلوط شدن cache)', async ({ page }) => {
    await loginAs(page, USER.full, COMPANIES.c1);
    await page.getByRole('button', { name: 'انتخاب شرکت فعال' }).click();
    await page.getByRole('button', { name: /بام پارس/ }).click();
    await page.waitForURL((url) => url.pathname === '/');
    await expect(page.getByText('۹۰۰٬۰۰۰').first()).toBeVisible({ timeout: 10_000 });

    await page.getByRole('button', { name: 'انتخاب شرکت فعال' }).click();
    await page.getByRole('button', { name: /آریا صنعت/ }).click();
    await page.waitForURL((url) => url.pathname === '/');
    await expect(page.getByText('۵٬۰۰۰٬۰۰۰').first()).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('۹۰۰٬۰۰۰')).toHaveCount(0);
  });

  test('خروج از حساب → نشست پاک و بازگشت به login؛ داده شرکت از دسترس خارج می‌شود', async ({ page }) => {
    await loginAs(page, USER.full, COMPANIES.c1);
    await page.getByRole('button', { name: 'منوی کاربری' }).click();
    await page.getByRole('button', { name: 'خروج از حساب' }).click();
    await expect(page).toHaveURL(/\/login/, { timeout: 10_000 });

    // بازگشت مستقیم به داشبورد → دوباره login
    await page.goto('/accounting/documents');
    await expect(page).toHaveURL(/\/login/);
  });
});
