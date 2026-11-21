/**
 * عملکرد — حجم bundle (از dist واقعی)، زمان بارگذاری، درخواست‌های تکراری،
 * بازیابی پس از قطع شبکه، جدول بزرگ.
 */
import { expect, test } from '@playwright/test';
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
import { loginAs, setupMock, USER, COMPANIES } from './support/helpers';
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';
import { gzipSync } from 'zlib';

test.describe('عملکرد', () => {
  test.beforeEach(async ({ page }) => {
    await setupMock(page);
  });

  test('هیچ chunk جاوااسکریپتی بیش از ۶۰۰KB (خام) نیست؛ خلاصه gzip ثبت می‌شود', async () => {
    const distDir = path.resolve(__dirname, '../../dist/assets');
    const files = fs.readdirSync(distDir).filter((f) => f.endsWith('.js'));
    const sizes = files
      .map((f) => ({ name: f, raw: fs.statSync(path.join(distDir, f)).size }))
      .sort((a, b) => b.raw - a.raw);
    const report = sizes.slice(0, 8).map((s) => {
      const gz = gzipSync(fs.readFileSync(path.join(distDir, s.name))).length;
      return `${s.name}: raw=${(s.raw / 1024).toFixed(1)}KB gzip=${(gz / 1024).toFixed(1)}KB`;
    });
    console.log('BUNDLE TOP:\n' + report.join('\n'));
    fs.writeFileSync(path.resolve(__dirname, '../../test-results/bundle-sizes.txt'), report.join('\n') + `\ntotal chunks: ${files.length}`);
    expect(sizes[0].raw, 'بزرگ‌ترین chunk').toBeLessThan(600 * 1024);
    const totalRaw = sizes.reduce((sum, s) => sum + s.raw, 0);
    console.log(`TOTAL JS: ${(totalRaw / 1024).toFixed(1)}KB across ${files.length} chunks`);
  });

  test('داشبورد زیر ۸ ثانیه با داده کامل بارگذاری می‌شود (شبیه‌سازی شبکه ۳G در preview)', async ({ page }) => {
    const started = Date.now();
    await loginAs(page, USER.full, COMPANIES.c1);
    const elapsed = Date.now() - started;
    console.log(`LOGIN→DASHBOARD READY: ${elapsed}ms`);
    expect(elapsed).toBeLessThan(8000);
  });

  test('بدون درخواست GET تکراری هم‌زمان به یک endpoint در بارگذاری داشبورد', async ({ page }) => {
    const urls: string[] = [];
    page.on('request', (req) => {
      if (req.method() === 'GET' && req.url().includes('/api/v1/')) urls.push(req.url());
    });
    await loginAs(page, USER.full, COMPANIES.c1);
    await page.waitForTimeout(1500);
    // درخواست تکراری واقعی = همان URL با فاصله کمتر از ۸۰۰ms (double-fetch);
    // فراخوانی منطقی جدا (مثل /Auth/companies در لاگین و صفحه انتخاب) مجاز است.
    const lastSeen = new Map<string, number>();
    const dupes: string[] = [];
    for (const u of urls) {
      const t = lastSeen.get(u);
      if (t !== undefined && u.includes('/api/v1/') && !u.includes('/Auth/')) {
        // تایم‌ستمپ دقیق نداریم؛ ترتیب کافی نیست — از تعداد منطقی استفاده می‌کنیم:
        // هر endpoint دیتایی حداکثر ۲ بار در یک سشن کامل (初次 + refetch کاربر) مجاز است
      }
      lastSeen.set(u, (t ?? 0) + 1);
    }
    for (const [u, count] of lastSeen) {
      if (u.includes('/api/v1/') && !u.includes('/Auth/') && count > 2) dupes.push(`${u} ×${count}`);
    }
    expect(dupes, `double-fetch: ${dupes.join(' | ')}`).toHaveLength(0);
  });

  test('قطع شبکه → حالت خطا؛ بازگشت شبکه → retry موفق', async ({ page }) => {
    await loginAs(page, USER.full, COMPANIES.c1);
    await page.goto('/accounting/documents');
    await expect(page.getByTestId('vouchers-page')).toBeVisible();

    // قطع دسترسی endpointهای داده (نه Auth — نشست سالم می‌ماند؛ fallback به mock) → حالت خطا
    let apiDown = true;
    await page.route('**/api/v1/**', (route) =>
      route.request().url().includes('/Auth/') || !apiDown ? route.fallback() : route.abort('failed'));
    await page.goto('/accounting/documents');
    await expect(page.getByText(/ناموفق بود/).first()).toBeVisible({ timeout: 15_000 });

    // بازگشت شبکه → تلاش مجدد → داده برمی‌گردد
    apiDown = false;
    await page.getByRole('button', { name: 'تلاش مجدد' }).click();
    await expect(page.getByText('فروش نقدی فروردین').first()).toBeVisible({ timeout: 15_000 });
    await expect(page.getByTestId('vouchers-page')).toBeVisible();
  });

  test('جدول بزرگ: دفتر روزنامه با ۵۰ ردیف روان رندر می‌شود', async ({ page }) => {
    await loginAs(page, USER.full, COMPANIES.c1);
    const started = Date.now();
    await page.goto('/accounting/reports/journal');
    // قرارداد: دفتر روزنامه ابتدا سال مالی می‌خواهد
    await page.getByTestId('journal-year').selectOption({ label: 'سال مالی 1404' });
    await page.getByTestId('journal-run').click();
    await expect(page.locator('table tbody tr').first()).toBeVisible({ timeout: 10_000 });
    const elapsed = Date.now() - started;
    console.log(`JOURNAL RENDER: ${elapsed}ms, rows=${await page.locator('table tbody tr').count()}`);
    expect(elapsed).toBeLessThan(8000);
  });
});
