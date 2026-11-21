/**
 * سناریوی ۳ — تأیید و ثبت قطعی توسط کاربر مجاز (SOD: ایجادکننده ≠ تأییدکننده)
 * و مشاهده گردش در دفتر کل و تراکنش‌های حساب.
 */
import { expect, test } from '@playwright/test';
import { loginAs, setupMock, USER, COMPANIES } from './support/helpers';
import { installMockBackend } from './support/mock-backend';

const pickAccount = async (page: import('@playwright/test').Page, row: number, accountName: string) => {
  await page.getByTestId(`line-account-picker-${row}`).click();
  const input = page.getByTestId(`line-account-picker-${row}`).getByRole('textbox');
  await input.fill(accountName.slice(0, 4));
  const option = page.getByRole('option').filter({ hasText: accountName }).first();
  await option.waitFor({ state: 'visible', timeout: 10_000 });
  await option.getByRole('button').click();
};

const createAndSubmitDoc = async (page: import('@playwright/test').Page, description: string, amount: string) => {
  await page.goto('/accounting/documents/new');
  await page.getByTestId('voucher-date').fill('2026-04-02');
  await page.getByTestId('voucher-description').fill(description);
  await pickAccount(page, 1, 'صندوق');
  await page.getByTestId('line-debit-1').fill(amount);
  await page.getByLabel('شرح ردیف 1').fill('دریافت از مشتری');
  await page.getByTestId('add-row').click();
  await pickAccount(page, 2, 'درآمد فروش');
  await page.getByTestId('line-credit-2').fill(amount);
  await page.getByLabel('شرح ردیف 2').fill('درآمد فروش سند E2E');
  await page.getByTestId('save-draft').click();
  await page.waitForURL(/\/accounting\/documents\/(doc-.*)$/);
  const docUrl = page.url();
  await page.getByTestId('action-submit').click();
  await page.getByRole('button', { name: 'ارسال', exact: true }).click();
  await expect(page.getByText('در انتظار تأیید').first()).toBeVisible({ timeout: 10_000 });
  return docUrl;
};

test.describe('سناریوی ۳ — تأیید، ثبت قطعی، دفتر کل', () => {
  test.beforeEach(async ({ page }) => {
    await setupMock(page);
  });

  test('ایجادکننده نمی‌تواند تأیید کند (SOD)؛ مدیر مالی تأیید و ثبت قطعی می‌کند', async ({ page, context }) => {
    // ── حسابدار: ایجاد و ارسال ──
    await loginAs(page, USER.full, COMPANIES.c1);
    const docUrl = await createAndSubmitDoc(page, 'فروش اعتباری E2E', '850000.25');
    // تلاش ایجادکننده برای تأیید → خطای SOD از backend (قرارداد واقعی)
    await page.getByTestId('action-approve').click();
    await page.getByRole('button', { name: 'تأیید سند', exact: true }).click();
    await expect(page.getByText(/تفکیک وظایف/)).toBeVisible({ timeout: 10_000 });
    await page.keyboard.press('Escape');

    // ── مدیر مالی: تأیید + ثبت قطعی ──
    const approverContext = await context.browser()?.newContext();
    const approver = await approverContext!.newPage();
    // توجه: resetState نمی‌زنیم — اسناد ساخته‌شده باید باقی بمانند؛ فقط interception نصب می‌شود
    await installMockBackend(approver);
    await loginAs(approver, USER.manager, COMPANIES.c1);
    await approver.goto(docUrl);
    await expect(approver.getByTestId('voucher-detail-page')).toBeVisible();
    await approver.getByTestId('action-approve').click();
    await approver.getByRole('button', { name: 'تأیید سند', exact: true }).click();
    await expect(approver.getByText('تاییدشده').first()).toBeVisible({ timeout: 10_000 });
    await approver.getByTestId('action-post').click();
    await approver.getByRole('button', { name: 'ثبت قطعی', exact: true }).click();
    await expect(approver.getByText('ثبت قطعی').first()).toBeVisible({ timeout: 10_000 });
    await expect(approver.getByTestId('posted-readonly-note')).toBeVisible();
    const docNumber = await approver.getByText(/^سند شماره/).first().textContent().catch(() => null);
    void docNumber;
    await approverContext!.close().catch(() => {});
  });

  test('گردش سند ثبت‌شده در دفتر کل و تراکنش‌های حساب دیده می‌شود', async ({ page, context }) => {
    await loginAs(page, USER.full, COMPANIES.c1);
    const docUrl = await createAndSubmitDoc(page, 'فروش نقدی برای دفتر کل', '600000');
    // تأیید و ثبت با مدیر
    const approverContext = await context.browser()?.newContext();
    const approver = await approverContext!.newPage();
    // توجه: resetState نمی‌زنیم — اسناد ساخته‌شده باید باقی بمانند؛ فقط interception نصب می‌شود
    await installMockBackend(approver);
    await loginAs(approver, USER.manager, COMPANIES.c1);
    await approver.goto(docUrl);
    await approver.getByTestId('action-approve').click();
    await approver.getByRole('button', { name: 'تأیید سند', exact: true }).click();
    await expect(approver.getByText('تاییدشده').first()).toBeVisible({ timeout: 10_000 });
    await approver.getByTestId('action-post').click();
    await approver.getByRole('button', { name: 'ثبت قطعی', exact: true }).click();
    await expect(approver.getByText('ثبت قطعی').first()).toBeVisible({ timeout: 10_000 });
    await approverContext!.close().catch(() => {});

    // دفتر کل — جمع گردش بدهکار = ۵٬۰۰۰٬۰۰۰ (seed) + ۶۰۰٬۰۰۰ (سند جدید) = ۵٬۶۰۰٬۰۰۰
    await page.goto('/accounting/reports/general-ledger');
    await page.getByTestId('gl-year').selectOption({ label: 'سال مالی 1404' });
    await page.getByTestId('gl-run').click();
    await expect(page.getByTestId('gl-total-debit')).toContainText('5,600,000', { timeout: 10_000 });
    // تراکنش‌های حساب صندوق — شرح سند جدید در گردش
    await page.goto('/accounting/reports/general-ledger/acc-cash?fiscalYearId=fy-1404');
    await expect(page.getByText('فروش نقدی برای دفتر کل')).toBeVisible({ timeout: 10_000 });
  });
});
