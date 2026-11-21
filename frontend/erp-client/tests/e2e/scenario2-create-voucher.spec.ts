/**
 * سناریوی ۲ — ایجاد سند: ردیف‌ها، انتخاب حساب از picker، بدهکار/بستانکار،
 * توازن زنده، ذخیره پیش‌نویس، ارسال برای تأیید.
 */
import { expect, test } from '@playwright/test';
import { loginAs, setupMock, USER, COMPANIES } from './support/helpers';

const pickAccount = async (page: import('@playwright/test').Page, row: number, accountName: string) => {
  await page.getByTestId(`line-account-picker-${row}`).click();
  const input = page.getByTestId(`line-account-picker-${row}`).getByRole('textbox');
  await input.fill(accountName.slice(0, 4));
  const option = page.getByRole('option').filter({ hasText: accountName }).first();
  await option.waitFor({ state: 'visible', timeout: 10_000 });
  await option.getByRole('button').click();
};

test.describe('سناریوی ۲ — ایجاد و ارسال سند', () => {
  test.beforeEach(async ({ page }) => {
    await setupMock(page);
    await loginAs(page, USER.full, COMPANIES.c1);
  });

  test('ایجاد سند متوازن → ذخیره پیش‌نویس → ارسال برای تأیید', async ({ page }) => {
    await page.goto('/accounting/documents/new');
    await expect(page.getByTestId('voucher-form')).toBeVisible();

    // سربرگ سند — تاریخ داخل دوره باز (فروردین ۱۴۰۴)
    await page.getByTestId('voucher-date').fill('2026-04-01');
    await page.getByTestId('voucher-description').fill('فروش نقدی روز — E2E');

    // ردیف اول: بدهکار صندوق
    await pickAccount(page, 1, 'صندوق');
    await page.getByTestId('line-debit-1').fill('1250000.50');
    await page.getByLabel('شرح ردیف 1').fill('دریافت نقدی از مشتری');

    // ردیف دوم: بستانکار درآمد فروش
    await page.getByTestId('add-row').click();
    await pickAccount(page, 2, 'درآمد فروش');
    await page.getByTestId('line-credit-2').fill('1250000.50');
    await page.getByLabel('شرح ردیف 2').fill('شناسایی درآمد');

    // توازن زنده
    await expect(page.getByTestId('balanced-badge')).toBeVisible();
    await expect(page.getByTestId('total-difference')).toHaveText('0');

    // ذخیره پیش‌نویس → هدایت به جزئیات سند
    await page.getByTestId('save-draft').click();
    await page.waitForURL(/\/accounting\/documents\/doc-/);
    await expect(page.getByTestId('voucher-detail-page')).toBeVisible();
    await expect(page.getByText('پیش‌نویس').first()).toBeVisible();
    await expect(page.getByTestId('view-total-debit')).toContainText('1,250,000.5');

    // ارسال برای تأیید — دیالوگ تأیید
    await page.getByTestId('action-submit').click();
    await page.getByRole('button', { name: 'ارسال', exact: true }).click();
    await expect(page.getByText('در انتظار تأیید').first()).toBeVisible({ timeout: 10_000 });
    // سازنده دیگر دکمه ویرایش ندارد
    await expect(page.getByTestId('edit-draft')).not.toBeVisible();
  });

  test('سند نامتوازن اجازه ذخیره ندارد و پیام اعتبارسنجی می‌آید', async ({ page }) => {
    await page.goto('/accounting/documents/new');
    await page.getByTestId('voucher-date').fill('2026-04-01');
    await page.getByTestId('voucher-description').fill('سند نامتوازن E2E');
    await pickAccount(page, 1, 'صندوق');
    await page.getByTestId('line-debit-1').fill('500000');
    await expect(page.getByTestId('unbalanced-badge')).toBeVisible();
    await page.getByTestId('save-draft').click();
    await expect(page.getByTestId('form-errors')).toBeVisible();
    // هنوز روی فرم هستیم — سند ساخته نشده
    expect(page.url()).toContain('/documents/new');
  });
});
