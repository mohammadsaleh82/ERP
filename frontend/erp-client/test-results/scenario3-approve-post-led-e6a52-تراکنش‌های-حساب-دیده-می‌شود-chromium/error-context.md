# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: scenario3-approve-post-ledger.spec.ts >> سناریوی ۳ — تأیید، ثبت قطعی، دفتر کل >> گردش سند ثبت‌شده در دفتر کل و تراکنش‌های حساب دیده می‌شود
- Location: tests/e2e/scenario3-approve-post-ledger.spec.ts:73:3

# Error details

```
Error: expect(locator).toContainText(expected) failed

Locator: getByTestId('gl-total-debit')
Expected substring: "5,600,000"
Received string:    "5,000,000"
Timeout: 10000ms

Call log:
  - Expect "toContainText" getByTestId('gl-total-debit') with timeout 10000ms
  - waiting for getByTestId('gl-total-debit')
    24 × locator resolved to <span dir="ltr" data-testid="gl-total-debit" class="inline-block tabular-nums text-sky-700 dark:text-sky-400">5,000,000</span>
       - unexpected value "5,000,000"

```

```yaml
- text: 5,000,000
```

# Test source

```ts
  1   | /**
  2   |  * سناریوی ۳ — تأیید و ثبت قطعی توسط کاربر مجاز (SOD: ایجادکننده ≠ تأییدکننده)
  3   |  * و مشاهده گردش در دفتر کل و تراکنش‌های حساب.
  4   |  */
  5   | import { expect, test } from '@playwright/test';
  6   | import { loginAs, setupMock, USER, COMPANIES } from './support/helpers';
  7   | import { installMockBackend } from './support/mock-backend';
  8   | 
  9   | const pickAccount = async (page: import('@playwright/test').Page, row: number, accountName: string) => {
  10  |   await page.getByTestId(`line-account-picker-${row}`).click();
  11  |   const input = page.getByTestId(`line-account-picker-${row}`).getByRole('textbox');
  12  |   await input.fill(accountName.slice(0, 4));
  13  |   const option = page.getByRole('option').filter({ hasText: accountName }).first();
  14  |   await option.waitFor({ state: 'visible', timeout: 10_000 });
  15  |   await option.getByRole('button').click();
  16  | };
  17  | 
  18  | const createAndSubmitDoc = async (page: import('@playwright/test').Page, description: string, amount: string) => {
  19  |   await page.goto('/accounting/documents/new');
  20  |   await page.getByTestId('voucher-date').fill('2026-04-02');
  21  |   await page.getByTestId('voucher-description').fill(description);
  22  |   await pickAccount(page, 1, 'صندوق');
  23  |   await page.getByTestId('line-debit-1').fill(amount);
  24  |   await page.getByLabel('شرح ردیف 1').fill('دریافت از مشتری');
  25  |   await page.getByTestId('add-row').click();
  26  |   await pickAccount(page, 2, 'درآمد فروش');
  27  |   await page.getByTestId('line-credit-2').fill(amount);
  28  |   await page.getByLabel('شرح ردیف 2').fill('درآمد فروش سند E2E');
  29  |   await page.getByTestId('save-draft').click();
  30  |   await page.waitForURL(/\/accounting\/documents\/(doc-.*)$/);
  31  |   const docUrl = page.url();
  32  |   await page.getByTestId('action-submit').click();
  33  |   await page.getByRole('button', { name: 'ارسال', exact: true }).click();
  34  |   await expect(page.getByText('در انتظار تأیید').first()).toBeVisible({ timeout: 10_000 });
  35  |   return docUrl;
  36  | };
  37  | 
  38  | test.describe('سناریوی ۳ — تأیید، ثبت قطعی، دفتر کل', () => {
  39  |   test.beforeEach(async ({ page }) => {
  40  |     await setupMock(page);
  41  |   });
  42  | 
  43  |   test('ایجادکننده نمی‌تواند تأیید کند (SOD)؛ مدیر مالی تأیید و ثبت قطعی می‌کند', async ({ page, context }) => {
  44  |     // ── حسابدار: ایجاد و ارسال ──
  45  |     await loginAs(page, USER.full, COMPANIES.c1);
  46  |     const docUrl = await createAndSubmitDoc(page, 'فروش اعتباری E2E', '850000.25');
  47  |     // تلاش ایجادکننده برای تأیید → خطای SOD از backend (قرارداد واقعی)
  48  |     await page.getByTestId('action-approve').click();
  49  |     await page.getByRole('button', { name: 'تأیید سند', exact: true }).click();
  50  |     await expect(page.getByText(/تفکیک وظایف/)).toBeVisible({ timeout: 10_000 });
  51  |     await page.keyboard.press('Escape');
  52  | 
  53  |     // ── مدیر مالی: تأیید + ثبت قطعی ──
  54  |     const approverContext = await context.browser()?.newContext();
  55  |     const approver = await approverContext!.newPage();
  56  |     // توجه: resetState نمی‌زنیم — اسناد ساخته‌شده باید باقی بمانند؛ فقط interception نصب می‌شود
  57  |     await installMockBackend(approver);
  58  |     await loginAs(approver, USER.manager, COMPANIES.c1);
  59  |     await approver.goto(docUrl);
  60  |     await expect(approver.getByTestId('voucher-detail-page')).toBeVisible();
  61  |     await approver.getByTestId('action-approve').click();
  62  |     await approver.getByRole('button', { name: 'تأیید سند', exact: true }).click();
  63  |     await expect(approver.getByText('تاییدشده').first()).toBeVisible({ timeout: 10_000 });
  64  |     await approver.getByTestId('action-post').click();
  65  |     await approver.getByRole('button', { name: 'ثبت قطعی', exact: true }).click();
  66  |     await expect(approver.getByText('ثبت قطعی').first()).toBeVisible({ timeout: 10_000 });
  67  |     await expect(approver.getByTestId('posted-readonly-note')).toBeVisible();
  68  |     const docNumber = await approver.getByText(/^سند شماره/).first().textContent().catch(() => null);
  69  |     void docNumber;
  70  |     await approverContext!.close().catch(() => {});
  71  |   });
  72  | 
  73  |   test('گردش سند ثبت‌شده در دفتر کل و تراکنش‌های حساب دیده می‌شود', async ({ page, context }) => {
  74  |     await loginAs(page, USER.full, COMPANIES.c1);
  75  |     const docUrl = await createAndSubmitDoc(page, 'فروش نقدی برای دفتر کل', '600000');
  76  |     // تأیید و ثبت با مدیر
  77  |     const approverContext = await context.browser()?.newContext();
  78  |     const approver = await approverContext!.newPage();
  79  |     // توجه: resetState نمی‌زنیم — اسناد ساخته‌شده باید باقی بمانند؛ فقط interception نصب می‌شود
  80  |     await installMockBackend(approver);
  81  |     await loginAs(approver, USER.manager, COMPANIES.c1);
  82  |     await approver.goto(docUrl);
  83  |     await approver.getByTestId('action-approve').click();
  84  |     await approver.getByRole('button', { name: 'تأیید سند', exact: true }).click();
  85  |     await expect(approver.getByText('تاییدشده').first()).toBeVisible({ timeout: 10_000 });
  86  |     await approver.getByTestId('action-post').click();
  87  |     await approver.getByRole('button', { name: 'ثبت قطعی', exact: true }).click();
  88  |     await expect(approver.getByText('ثبت قطعی').first()).toBeVisible({ timeout: 10_000 });
  89  |     await approverContext!.close().catch(() => {});
  90  | 
  91  |     // دفتر کل — جمع گردش بدهکار = ۵٬۰۰۰٬۰۰۰ (seed) + ۶۰۰٬۰۰۰ (سند جدید) = ۵٬۶۰۰٬۰۰۰
  92  |     await page.goto('/accounting/reports/general-ledger');
  93  |     await page.getByTestId('gl-year').selectOption({ label: 'سال مالی 1404' });
  94  |     await page.getByTestId('gl-run').click();
> 95  |     await expect(page.getByTestId('gl-total-debit')).toContainText('5,600,000', { timeout: 10_000 });
      |                                                      ^ Error: expect(locator).toContainText(expected) failed
  96  |     // تراکنش‌های حساب صندوق — شرح سند جدید در گردش
  97  |     await page.goto('/accounting/reports/general-ledger/acc-cash?fiscalYearId=fy-1404');
  98  |     await expect(page.getByText('فروش نقدی برای دفتر کل')).toBeVisible({ timeout: 10_000 });
  99  |   });
  100 | });
  101 | 
```