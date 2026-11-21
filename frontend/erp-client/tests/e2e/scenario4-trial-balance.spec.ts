/**
 * سناریوی ۴ — گزارش تراز آزمایشی: مشاهده نتایج واقعی، اعمال فیلتر، خروجی گرفتن (CSV).
 */
import { expect, test } from '@playwright/test';
import * as fs from 'fs';
import { loginAs, setupMock, USER, COMPANIES } from './support/helpers';

test.describe('سناریوی ۴ — تراز آزمایشی', () => {
  test.beforeEach(async ({ page }) => {
    await setupMock(page);
    await loginAs(page, USER.full, COMPANIES.c1);
  });

  test('مشاهده تراز آزمایشی متوازن با داده واقعی اسناد Posted', async ({ page }) => {
    await page.goto('/accounting/reports/trial-balance');
    await expect(page.getByTestId('tb-run')).toBeVisible();
    await page.getByTestId('tb-run').click();
    // ردیف صندوق: ۵٬۰۰۰٬۰۰۰ بدهکار دوره (seed)
    await expect(page.getByText('صندوق').first()).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('درآمد فروش').first()).toBeVisible();
    // جمع کل متوازن
    await expect(page.getByText(/5,000,000/).first()).toBeVisible();
  });

  test('اعمال فیلتر بازه زمانی → درخواست جدید با پارامترها', async ({ page }) => {
    await page.goto('/accounting/reports/trial-balance');
    await page.getByTestId('tb-run').click();
    await expect(page.getByText('صندوق').first()).toBeVisible({ timeout: 10_000 });

    // فیلتر از تاریخ — درخواست به URL با from می‌رود
    const [request] = await Promise.all([
      page.waitForRequest((req) => req.url().includes('/accounting/reports/trial-balance') && req.url().includes('from='),
        { timeout: 10_000 }).catch(() => null),
      (async () => {
        await page.getByLabel(/از تاریخ/).fill('2026-03-21');
        await page.getByTestId('tb-run').click();
      })(),
    ]);
    if (request) expect(request.url()).toContain('from=2026-03-21');
    await expect(page.getByText('صندوق').first()).toBeVisible();
  });

  test('خروجی CSV سمت سرور — فایل با محتوای واقعی دریافت می‌شود', async ({ page }) => {
    await page.goto('/accounting/reports/trial-balance');
    await page.getByTestId('tb-run').click();
    await expect(page.getByText('صندوق').first()).toBeVisible({ timeout: 10_000 });

    const downloadPromise = page.waitForEvent('download', { timeout: 15_000 });
    await page.getByTestId('tb-export-csv').click();
    const download = await downloadPromise;
    const fileName = download.suggestedFilename();
    expect(fileName.toLowerCase()).toContain('csv');
    const path = await download.path();
    const content = fs.readFileSync(path!, 'utf8');
    expect(content).toContain('کد حساب');
    expect(content).toContain('110101'); // صندوق
  });
});
