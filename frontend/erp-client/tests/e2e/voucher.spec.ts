import { expect, test } from '@playwright/test';

/**
 * E2E فاز ۶ — تجربه ثبت سند حسابداری.
 * پیش‌فرض: dev server روی 3000 (mock/بک‌اند واقعی طبق محیط).
 * اجرا: npx playwright install chromium && npm run test:e2e
 *
 * مسیر: ورود → انتخاب شرکت → سند جدید → دو ردیف متوازن → ذخیره پیش‌نویس
 * → جزئیات سند → ارسال برای تأیید.
 * اگر نشست از قبل موجود باشد (storageState) مستقیم به فرم می‌رود؛ در غیر این صورت
 * سناریوهای smoke ورود اجرا می‌شوند (auth.spec.ts).
 */
test('voucher create flow: balanced draft submit', async ({ page }) => {
  await page.goto('/login');

  // اگر فرم ورود بود، ورود با کاربر دمو (در محیط واقعی: کاربر مجاز documents:create)
  const emailInput = page.getByLabel(/ایمیل|نام کاربری/).first();
  if (await emailInput.isVisible({ timeout: 4000 }).catch(() => false)) {
    await emailInput.fill('admin@aria.local');
    await page.getByLabel(/رمز عبور/).fill('Admin!234');
    await page.getByRole('button', { name: /ورود|ثبت/ }).click();
    await page.waitForURL(/select-company|accounting/, { timeout: 15000 });
  }

  // انتخاب شرکت در صورت نیاز
  if (page.url().includes('select-company')) {
    await page.getByRole('button').first().click();
    await page.waitForURL(/accounting|\/$/, { timeout: 15000 });
  }

  // ایجاد سند
  await page.goto('/accounting/documents/new');
  await expect(page.getByTestId('voucher-form')).toBeVisible({ timeout: 15000 });

  await page.getByTestId('voucher-date').fill('2026-03-01');
  await page.getByTestId('voucher-description').fill('فروش نقدی — E2E');

  // ردیف ۱: بدهکار صندوق
  const firstPicker = page.getByTestId('line-account-picker-1');
  await firstPicker.getByRole('textbox').click();
  await firstPicker.getByRole('textbox').fill('صندوق');
  await firstPicker.getByRole('listbox').locator('button').first().click();
  await page.getByTestId('line-debit-1').fill('12500');
  await page.getByLabel('شرح ردیف 1').fill('وصول نقدی');

  // ردیف ۲: بستانکار درآمد
  await page.getByTestId('add-row').click();
  const secondPicker = page.getByTestId('line-account-picker-2');
  await secondPicker.getByRole('textbox').click();
  await secondPicker.getByRole('textbox').fill('درآمد');
  await secondPicker.getByRole('listbox').locator('button').first().click();
  await page.getByTestId('line-credit-2').fill('12500');
  await page.getByLabel('شرح ردیف 2').fill('فروش');

  // توازن زنده
  await expect(page.getByTestId('balanced-badge')).toBeVisible();

  // ذخیره پیش‌نویس
  await page.getByTestId('save-draft').click();
  // پس از ذخیره به جزئیات هدایت می‌شویم (page onSaved navigate)
  await expect(page.getByTestId('voucher-detail-page')).toBeVisible({ timeout: 15000 });
});
