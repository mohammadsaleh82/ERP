/**
 * تست بصری — screenshot از صفحات اصلی در Desktop/Tablet/Mobile + RTL + فرم بلند +
 * dialog + صفحات خالی/خطا + بدون اسکرول افقی در 360px. خروجی: tests/e2e/screenshots/
 */
import { expect, test } from '@playwright/test';
import { loginAs, setupMock, collectConsoleErrors, criticalErrors, USER, COMPANIES } from './support/helpers';

const SHOT_DIR = 'tests/e2e/screenshots';

const noHorizontalOverflow = async (page: import('@playwright/test').Page) => {
  const result = await page.evaluate(() => ({
    scrollW: document.scrollingElement?.scrollWidth ?? 0,
    clientW: document.scrollingElement?.clientWidth ?? 0,
  }));
  expect(result.scrollW, `اسکرول افقی غیرضروری: ${result.scrollW} > ${result.clientW}`).toBeLessThanOrEqual(result.clientW + 1);
};

test.describe('تست بصری — desktop 1440', () => {
  test.use({ viewport: { width: 1440, height: 900 } });

  test.beforeEach(async ({ page }) => {
    await setupMock(page);
  });

  test('login / dashboard / vouchers / voucher-form / trial-balance / accounts', async ({ page }) => {
    const errors = collectConsoleErrors(page);
    await page.goto('/login');
    await page.screenshot({ path: `${SHOT_DIR}/desktop-login.png`, fullPage: true });

    await loginAs(page, USER.full, COMPANIES.c1);
    await page.waitForTimeout(600);
    await page.screenshot({ path: `${SHOT_DIR}/desktop-dashboard.png`, fullPage: true });

    await page.goto('/accounting/documents');
    await expect(page.getByTestId('vouchers-page')).toBeVisible();
    await page.waitForTimeout(500);
    await page.screenshot({ path: `${SHOT_DIR}/desktop-vouchers.png`, fullPage: true });

    await page.goto('/accounting/documents/new');
    await expect(page.getByTestId('voucher-form')).toBeVisible();
    // فرم بلند: دو ردیف + سربرگ برای حالت طولانی
    await page.getByTestId('voucher-date').fill('2026-04-01');
    await page.getByTestId('voucher-description').fill('سند نمونه برای بررسی بصری فرم');
    await page.getByTestId('add-row').click();
    await page.getByTestId('add-row').click();
    await page.screenshot({ path: `${SHOT_DIR}/desktop-voucher-form-long.png`, fullPage: true });

    await page.goto('/accounting/reports/trial-balance');
    await page.getByTestId('tb-run').click();
    await expect(page.getByText('صندوق').first()).toBeVisible({ timeout: 10_000 });
    await page.screenshot({ path: `${SHOT_DIR}/desktop-trial-balance.png`, fullPage: true });

    await page.goto('/accounting/accounts');
    await expect(page.getByTestId('accounts-page')).toBeVisible();
    await page.screenshot({ path: `${SHOT_DIR}/desktop-accounts.png`, fullPage: true });

    // dialog — دیالوگ تأیید ارسال سند
    await page.goto('/accounting/documents/new');
    await page.getByTestId('voucher-date').fill('2026-04-01');
    await page.getByTestId('voucher-description').fill('dialog');
    await page.getByTestId('add-row').click();
    await page.getByTestId('save-draft').click();
    await expect(page.getByTestId('form-errors')).toBeVisible(); // ردیف ناقص → دیالوگ نه، خطا
    await page.screenshot({ path: `${SHOT_DIR}/desktop-form-error-state.png`, fullPage: true });

    expect(criticalErrors(errors), `console: ${criticalErrors(errors).join(' | ')}`).toHaveLength(0);
  });
});

test.describe('تست بصری — tablet 768', () => {
  test.use({ viewport: { width: 768, height: 1024 } });

  test('dashboard + voucher form', async ({ page }) => {
    await setupMock(page);
    await loginAs(page, USER.full, COMPANIES.c1);
    await page.waitForTimeout(500);
    await page.screenshot({ path: `${SHOT_DIR}/tablet-dashboard.png`, fullPage: true });
    await page.goto('/accounting/documents/new');
    await page.screenshot({ path: `${SHOT_DIR}/tablet-voucher-form.png`, fullPage: true });
    await noHorizontalOverflow(page);
  });
});

test.describe('تست بصری — mobile 360 (RTL)', () => {
  test.use({ viewport: { width: 360, height: 740 } });

  test('بدون اسکرول افقی در داشبورد/اسناد/فرم سد + کشوی ناوبری موبایل', async ({ page }) => {
    await setupMock(page);
    await page.goto('/login');
    await page.screenshot({ path: `${SHOT_DIR}/mobile-login.png`, fullPage: true });

    await loginAs(page, USER.full, COMPANIES.c1);
    await page.waitForTimeout(600);
    await noHorizontalOverflow(page);
    await page.screenshot({ path: `${SHOT_DIR}/mobile-dashboard.png`, fullPage: true });

    // کشوی ناوبری موبایل (فاز ۹) — جایگزین sidebar
    await page.getByTestId('mobile-nav-trigger').click();
    await expect(page.getByRole('dialog')).toContainText('ناوبری');
    await page.screenshot({ path: `${SHOT_DIR}/mobile-nav-drawer.png` });
    await page.keyboard.press('Escape');
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 5000 });

    await page.goto('/accounting/documents');
    await noHorizontalOverflow(page);
    await page.screenshot({ path: `${SHOT_DIR}/mobile-vouchers.png`, fullPage: true });

    await page.goto('/accounting/documents/new');
    await page.getByTestId('add-row').click();
    await noHorizontalOverflow(page);
    await page.screenshot({ path: `${SHOT_DIR}/mobile-voucher-form.png`, fullPage: true });
  });
});

test.describe('تست بصری — حالت‌های خالی و خطا', () => {
  test.use({ viewport: { width: 1440, height: 900 } });

  test('empty state جست‌وجو بدون نتیجه + error state با قطع API', async ({ page }) => {
    await setupMock(page);
    await loginAs(page, USER.full, COMPANIES.c1);

    // empty: جست‌وجوی سراسری بدون نتیجه
    await page.getByRole('button', { name: 'جست‌وجوی سراسری' }).click();
    await page.getByTestId('global-search-input').fill('zzz-no-match-999');
    await expect(page.getByText(/یافت نشد|موردی/).first()).toBeVisible({ timeout: 10_000 });
    await page.screenshot({ path: `${SHOT_DIR}/desktop-empty-search.png` });

    // error: endpoint داشبورد ۵۰۰ → ErrorState
    await page.route('**/api/v1/Ledger/balances', (route) => route.fulfill({ status: 500, contentType: 'application/json', body: JSON.stringify({ title: 'ServerError', detail: 'خطای شبیه‌سازی‌شده سرور' }) }));
    await page.goto('/');
    await expect(page.getByText(/ناموفق بود/).first()).toBeVisible({ timeout: 15_000 });
    await page.screenshot({ path: `${SHOT_DIR}/desktop-error-state.png`, fullPage: true });
  });
});
