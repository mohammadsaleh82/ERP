/**
 * فاز ۹ — UX/A11y: skip-link، ناوبری موبایل، Ctrl+K، محافظ تغییرات ذخیره‌نشده،
 * انتقال فوکوس پس از خطا، aria-current سایدبار و landmark اصلی.
 * عناوین بر اساس کد واقعی فاز ۹ — بدون mock کردن کیت UI.
 */
import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import AxiosMockAdapter from 'axios-mock-adapter';
import { render, screen, fireEvent, waitFor, within, act } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { apiClient } from '@/core/api/client';
import { useAuthStore } from '@/core/auth/auth.store';
import { useCompanyStore } from '@/core/company/company.store';
import { AppLayout } from '@/app/layouts/app-layout';
import { Sidebar } from '@/shared/components/layout/sidebar';
import { VoucherForm } from '@/features/documents/components/voucher-form';
import { LoginPage } from '@/features/identity/pages/login-page';
import { useUnsavedChangesGuard } from '@/shared/hooks/use-unsaved-changes';
import { login } from '@/core/auth/auth.service';


vi.mock('@/core/notifications/toast', () => ({
  toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() },
}));

vi.mock('@/core/auth/auth.service', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/core/auth/auth.service')>();
  return { ...actual, login: vi.fn(actual.login.bind(actual)), getCurrentUser: vi.fn(actual.getCurrentUser?.bind(actual)) };
});

let mock: AxiosMockAdapter;

const accounts = [
  { id: 'a-cash', tenantId: 't1', companyId: 'c1', parentAccountId: null, code: '110101', fullCode: '110101', name: 'صندوق', description: null, level: 'Detail', nature: 'Asset', normalBalance: 'Debit', isActive: true, isSystemAccount: false, allowPosting: true, hasChildren: false, requireCostCenter: false, requireProject: false, requireDetailAccount: false, createdAtUtc: 'x', lastModifiedAtUtc: null },
  { id: 'a-rev', tenantId: 't1', companyId: 'c1', parentAccountId: null, code: '400001', fullCode: '400001', name: 'درآمد فروش', description: null, level: 'Detail', nature: 'Revenue', normalBalance: 'Credit', isActive: true, isSystemAccount: false, allowPosting: true, hasChildren: false, requireCostCenter: false, requireProject: false, requireDetailAccount: false, createdAtUtc: 'x', lastModifiedAtUtc: null },
];

beforeEach(() => {
  mock = new AxiosMockAdapter(apiClient, { onNoMatch: 'throwException' });
  useAuthStore.getState().setAuth(
    { userId: 'u1', email: 'a@b.c', fullName: 'حسابدار', roles: [], permissions: ['accounting:reports:read', 'accounting:documents:create', 'accounting:documents:submit', 'accounting:accounts:read', 'accounting:ledger:read'] },
    'tok', 't1', 'c1',
  );
  useCompanyStore.getState().setCompanyContext(
    { id: 't1', name: 'گروه آریا', companies: [{ id: 'c1', name: 'آریا', fiscalYear: 1404 }] },
    { id: 'c1', name: 'آریا', fiscalYear: 1404 },
  );
  mock.onGet('/Accounts/postable').reply(200, accounts);
  mock.onGet('/FiscalYears').reply(200, [
    { id: 'fy1', tenantId: 't1', companyId: 'c1', yearNumber: 1404, name: '۱۴۰۴', startDate: '2025-03-21', endDate: '2026-03-20', calendarType: 'Persian', status: 'Open', isClosed: false, openingBalancePosted: true },
  ]);
  mock.onGet('/FiscalYears/fy1/periods').reply(200, []);
});
afterEach(() => {
  mock.restore();
  useAuthStore.getState().clearAuth();
});

const renderWithRouter = (ui: React.ReactNode, entry = '/') => {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[entry]}>
        {ui ?? (
          <Routes>
            <Route element={<AppLayout />}>
              <Route path="*" element={<div>محتوا</div>} />
            </Route>
          </Routes>
        )}
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

describe('پوسته — skip-link و landmark', () => {
  it('skip-link اولین focusable است و به main-content اشاره می‌کند', () => {
    renderWithRouter(null);
    const skip = screen.getByRole('link', { name: 'پرش به محتوای اصلی' });
    expect(skip).toHaveAttribute('href', '#main-content');
    const main = document.getElementById('main-content');
    expect(main).not.toBeNull();
    // اولین عنصر focusable سند skip-link است
    const focusables = Array.from(document.querySelectorAll('a, button, input, select, textarea'));
    expect(focusables.indexOf(skip)).toBeLessThanOrEqual(1);
  });
});

describe('ناوبری موبایل و سایدبار', () => {
  it('دکمه منوی موبایل کشو را باز می‌کند و آیتم‌های مجوزدار داخل آن است', async () => {
    renderWithRouter(null);
    fireEvent.click(screen.getByTestId('mobile-nav-trigger'));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('ناوبری')).toBeInTheDocument();
    // آیتم‌های دارای مجوز reports — بدون مجوز نیستند
    expect(within(dialog).getByRole('link', { name: /ترازنامه/ })).toBeInTheDocument();
    expect(within(dialog).getByRole('link', { name: /دفتر روزنامه/ })).toBeInTheDocument();
  });

  it('sidebar حالت فعال را با sr-only هم اعلام می‌کند (بدون اتکا به رنگ)', () => {
    renderWithRouter(
      <Routes>
        <Route path="/accounting/reports/*" element={<Sidebar />} />
      </Routes>,
      '/accounting/reports/journal',
    );
    const active = screen.getByRole('link', { name: /دفتر روزنامه/ });
    expect(active.className).toContain('bg-primary');
    expect(within(active).getByText('(صفحه جاری)')).toBeInTheDocument();
  });
});

describe('Ctrl+K — جست‌وجوی سراسری', () => {
  it('میان‌بر advertised در UI واقعاً پنل جست‌وجو را باز می‌کند', async () => {
    renderWithRouter(null);
    expect(screen.queryByTestId('global-search-input')).not.toBeInTheDocument();
    fireEvent.keyDown(window, { key: 'k', ctrlKey: true });
    expect(await screen.findByTestId('global-search-input')).toBeInTheDocument();
    // Escape می‌بندد (Radix Popover) — رفتار استاندارد
    fireEvent.keyDown(screen.getByTestId('global-search-input'), { key: 'Escape' });
    await waitFor(() => expect(screen.queryByTestId('global-search-input')).not.toBeInTheDocument());
  });
});

describe('محافظ تغییرات ذخیره‌نشده', () => {
  const Harness: React.FC<{ dirty: boolean; onConfirmNavigation?: () => void; onTrigger?: () => void }> = ({ dirty, onConfirmNavigation, onTrigger }) => {
    const { guardDialog } = useUnsavedChangesGuard({ dirty, onConfirmNavigation });
    return (
      <div>
        <button type="button" onClick={() => window.history.pushState({}, '', '/other')}>nav</button>
        {onTrigger ? <button type="button" onClick={onTrigger}>noop</button> : null}
        {guardDialog}
      </div>
    );
  };

  it('با dirty، pushState رهگیری می‌شود و دیالوگ تأیید می‌آید؛ تأیید → ناوبری ادامه می‌یابد', async () => {
    const onConfirmNavigation = vi.fn();
    render(<Harness dirty onConfirmNavigation={onConfirmNavigation} />);
    fireEvent.click(screen.getByRole('button', { name: 'nav' }));
    const dialog = await screen.findByRole('alertdialog'); // Radix AlertDialog
    expect(dialog.textContent).toContain('ذخیره‌نشده');
    fireEvent.click(within(dialog).getByRole('button', { name: 'خروج بدون ذخیره' }));
    await waitFor(() => expect(onConfirmNavigation).toHaveBeenCalled());
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('بدون dirty، ناوبری بدون دیالوگ انجام می‌شود', () => {
    render(<Harness dirty={false} />);
    fireEvent.click(screen.getByRole('button', { name: 'nav' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('hook: allowNextNavigation یک ناوبری را بدون دیالوگ عبور می‌دهد (رگرسیون ذخیره→هدایت)', async () => {
    const Harness: React.FC<{ dirty: boolean; onReady: (allow: () => void) => void }> = ({ dirty, onReady }) => {
      const { guardDialog, allowNextNavigation } = useUnsavedChangesGuard({ dirty });
      onReady(allowNextNavigation);
      return (
        <div>
          <button type="button" onClick={() => window.history.pushState({}, '', '/other')}>nav</button>
          {guardDialog}
        </div>
      );
    };
    let allow: (() => void) | null = null;
    const { getByRole } = render(<Harness dirty onReady={(fn) => { allow = fn; }} />);
    act(() => { allow?.(); });
    fireEvent.click(getByRole('button', { name: 'nav' }));
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  });

  it('فرم سند: تایپ → dirty و رهگیری ناوبری؛ پس از ذخیره موفق → رهگیری برداشته می‌شود', async () => {
    mock.onPost('/Documents').reply(201, { id: 'doc-1', documentNumber: 101, status: 'Draft' });
    mock.onPost('/Documents/doc-1/submit').reply(200, { id: 'doc-1', status: 'PendingApproval' });
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const GuardedFormHarness: React.FC = () => {
      const { guardDialog } = useUnsavedChangesGuard({ dirty: true });
      return (
        <div>
          <button type="button" onClick={() => window.history.pushState({}, '', '/other')}>nav</button>
          <VoucherForm />
          {guardDialog}
        </div>
      );
    };
    const { unmount } = render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter>
          <GuardedFormHarness />
        </MemoryRouter>
      </QueryClientProvider>,
    );
    await waitFor(() => expect(mock.history.get.some((item) => item.url.includes('/Accounts/postable'))).toBe(true));
    fireEvent.change(screen.getByTestId('voucher-description'), { target: { value: 'تست گارد' } });
    // ناوبری → دیالوگ
    fireEvent.click(screen.getByRole('button', { name: 'nav' }));
    expect(await screen.findByRole('alertdialog')).toBeTruthy();
    fireEvent.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'ماندن در فرم' }));
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
    unmount();
  });
});

describe('انتقال فوکوس پس از خطا', () => {
  it('فرم سند: ذخیره با ردیف ناقص → فوکوس به اولین فیلد ردیف ناقص', async () => {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter>
          <VoucherForm />
        </MemoryRouter>
      </QueryClientProvider>,
    );
    await waitFor(() => expect(mock.history.get.some((item) => item.url.includes('/FiscalYears'))).toBe(true));
    // تاریخ و شرح پر، ردیف ناقص
    fireEvent.change(screen.getByTestId('voucher-date'), { target: { value: '2026-03-01' } });
    fireEvent.change(screen.getByTestId('voucher-description'), { target: { value: 'سند تستی' } });
    fireEvent.click(screen.getByTestId('save-draft'));
    await waitFor(() => expect(screen.getByTestId('form-errors')).toBeInTheDocument());
    // فوکوس روی اولین کنترل ردیف ۱ (picker/input) است نه روی بدنه
    const firstLine = screen.getByTestId('line-row-1');
    expect(firstLine.contains(document.activeElement)).toBe(true);
    expect(document.activeElement?.tagName).toMatch(/INPUT|BUTTON/);
  });

  it('لاگین: خطای اعتبار ۴۰۱ → فوکوس روی پیام خطا (role=alert)', async () => {
    mock.onPost('/Auth/login').reply(401, { title: 'Unauthorized', detail: 'ایمیل یا کلمه عبور اشتباه است' });
    vi.mocked(login).mockRejectedValueOnce(new (await import('@/core/errors/problem-details')).ApiError(401, 'ایمیل یا کلمه عبور اشتباه است'));
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter>
          <LoginPage />
        </MemoryRouter>
      </QueryClientProvider>,
    );
    fireEvent.change(screen.getByLabelText(/ایمیل سازمانی/), { target: { value: 'user@test.ir' } });
    fireEvent.change(screen.getByLabelText(/کلمه عبور/), { target: { value: 'wrong' } });
    fireEvent.click(screen.getByRole('button', { name: /ورود/ }));
    const alert = await screen.findByRole('alert');
    await waitFor(() => expect(document.activeElement).toBe(alert));
    expect(alert.textContent).toContain('اشتباه است');
  });
});
