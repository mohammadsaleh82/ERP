/**
 * helpers مشترک E2E — جریان واقعی UI برای ورود و انتخاب شرکت + جمع‌آوری console errors.
 */
import { Page, expect } from '@playwright/test';
import { installMockBackend, resetState, users, companyRefs } from './mock-backend';

export const USER = users;
export const COMPANIES = companyRefs;

/** ورود واقعی از UI + انتخاب شرکت + رسیدن به داشبورد */
export const loginAs = async (page: Page, user: { email: string; password: string }, company = COMPANIES.c1) => {
  await page.goto('/login');
  await page.getByLabel(/ایمیل سازمانی/).fill(user.email);
  await page.getByLabel(/کلمه عبور/).fill(user.password);
  await page.getByRole('button', { name: /ورود/ }).click();
  await page.waitForURL(/\/select-company/);
  await page.getByRole('button', { name: new RegExp(company.name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')) }).first().click();
  await page.waitForURL((url) => url.pathname === '/');
  await expect(page.getByTestId('dashboard-page')).toBeVisible({ timeout: 15_000 });
};

/** نصب mock قراردادی + reset state — در beforeEach هر تست */
export const setupMock = async (page: Page) => {
  resetState();
  await installMockBackend(page);
};

/** جمع‌آوری خطاهای console — خطای بحرانی ممنوع */
export const collectConsoleErrors = (page: Page) => {
  const errors: string[] = [];
  page.on('console', (message) => {
    if (message.type() === 'error') errors.push(message.text());
  });
  page.on('pageerror', (error) => errors.push(`pageerror: ${error.message}`));
  return errors;
};

/** فیلتر نویزهای مجاز: favicon، سورس‌مپ، و 401 تلاش بازیابی نشست در boot (قرارداد صحیح — app مدیریتش می‌کند) */
export const criticalErrors = (errors: string[]) =>
  errors.filter((e) => !/favicon|sourcemap|DevTools/i.test(e) && !/Failed to load resource.*401/i.test(e));
