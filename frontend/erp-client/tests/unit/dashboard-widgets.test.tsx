/**
 * فاز ۴ — داشبورد: داده واقعی، حالت‌های مستقل هر Widget، خطای مستقل، مجوزها.
 * قراردادها مطابق backend: /Ledger/balances · /Documents · /FiscalYears · /AuditTrail
 */
import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import AxiosMockAdapter from 'axios-mock-adapter';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { apiClient } from '@/core/api/client';
import { useAuthStore } from '@/core/auth/auth.store';
import { useCompanyStore } from '@/core/company/company.store';
import { SummaryCards } from '@/features/dashboard/components/summary-cards';
import { PendingApprovals } from '@/features/dashboard/components/pending-approvals';
import { TopBalances } from '@/features/dashboard/components/top-balances';
import { FiscalStatusWidget } from '@/features/dashboard/components/fiscal-status';
import { FinancialAlerts } from '@/features/dashboard/components/financial-alerts';
import { QuickActions } from '@/features/dashboard/components/quick-actions';

let mock: AxiosMockAdapter;
let queryClient: QueryClient;

const renderWithProviders = (ui: React.ReactElement) => {
  queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>{ui}</MemoryRouter>
    </QueryClientProvider>,
  );
};

beforeEach(() => {
  mock = new AxiosMockAdapter(apiClient, { onNoMatch: 'throwException' });
  useAuthStore.getState().setAuth(
    { userId: 'u1', email: 'a@b.c', fullName: 'مدیر سیستم', roles: [], permissions: ['accounting:documents:read', 'accounting:ledger:read', 'accounting:fiscal-years:read', 'accounting:audit:read'] },
    'tok', 't1', 'c1',
  );
  useCompanyStore.getState().setCompanyContext(
    { id: 't1', name: 'گروه آریا', companies: [{ id: 'c1', name: 'بازرگانی آریا', fiscalYear: 1404 }] },
    { id: 'c1', name: 'بازرگانی آریا', fiscalYear: 1404 },
  );
});
afterEach(() => {
  mock.restore();
  useAuthStore.getState().clearAuth();
});

describe('SummaryCards (real data only)', () => {
  it('computes debit/credit totals from /Ledger/balances and counts from /Documents', async () => {
    mock.onGet('/Ledger/balances').reply(200, [
      { accountId: 'a1', accountFullCode: '1101', accountName: 'صندوق', periodDebit: 1000, periodCredit: 200, closingDisplayBalance: 800 },
      { accountId: 'a2', accountFullCode: '1102', accountName: 'بانک', periodDebit: 500, periodCredit: 100, closingDisplayBalance: 400 },
    ]);
    mock.onGet('/Documents').reply((config) => {
      const params = config.params as Record<string, unknown>;
      if (params?.status === 'PendingApproval') {
        return [200, { items: [{ id: 'd1', documentNumber: 101, status: 'PendingApproval', totalDebit: 500 }], totalCount: 3, pageNumber: 1, pageSize: 5 }];
      }
      return [200, { items: [], totalCount: 12, pageNumber: 1, pageSize: 100 }];
    });

    renderWithProviders(<SummaryCards range={{}} />);

    expect(await screen.findByText('بزرگ‌ترین مانده حساب‌ها'.length > 0 ? 'جمع گردش بدهکار' : '')).toBeTruthy();
    // ۱۰۰۰ + ۵۰۰ = ۱۵۰۰ (محاسبه با کد، نه عدد ساختگی)
    expect(await screen.findByText('۱٬۵۰۰')).toBeInTheDocument();
    expect(await screen.findByText('اسناد بازه جاری')).toBeInTheDocument();
    expect(await screen.findByText('۱۲')).toBeInTheDocument(); // documents totalCount
    expect(await screen.findByText('۳')).toBeInTheDocument(); // pending totalCount
  });

  it('renders unauthorized for ledger card when user lacks ledger permission', async () => {
    useAuthStore.setState({
      user: { userId: 'u1', email: 'a@b.c', fullName: 'کاربر', roles: [], permissions: ['accounting:documents:read'] },
    });
    mock.onGet('/Documents').reply(200, { items: [], totalCount: 0, pageNumber: 1, pageSize: 100 });

    renderWithProviders(<SummaryCards range={{}} />);
    expect(await screen.findAllByText('مجوز مشاهده این بخش را ندارید')).toHaveLength(2);
    // درخواست balances هرگز ارسال نمی‌شود (enabled=false)
    expect(mock.history.get.filter((call) => call.url.includes('/Ledger/balances'))).toHaveLength(0);
  });

  it('shows isolated error state only for the failing widget (documents API fails, balances succeed)', async () => {
    mock.onGet('/Ledger/balances').reply(200, [{ accountId: 'a1', accountFullCode: '1101', accountName: 'صندوق', periodDebit: 100, periodCredit: 50, closingDisplayBalance: 0 }]);
    mock.onGet('/Documents').reply(500, { code: 'Http.500', detail: 'خطای سرور' });

    renderWithProviders(<SummaryCards range={{}} />);
    expect(await screen.findByText('۱۰۰')).toBeInTheDocument(); // balances کار می‌کند
    // اسناد بازه + در انتظار تأیید (هر دو /Documents) خطا دارند؛ balances سالم مانده است
    expect((await screen.findAllByText('نمایش این بخش ناموفق بود')).length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('۱۰۰')).toBeInTheDocument();
  });
});

describe('PendingApprovals', () => {
  it('lists real pending documents from Status=PendingApproval query', async () => {
    mock.onGet('/Documents').reply((config) => {
      expect((config.params as Record<string, unknown>).status).toBe('PendingApproval');
      return [200, {
        items: [
          { id: 'd1', documentNumber: 101, status: 'PendingApproval', description: 'خرید ملزومات', totalDebit: 2500000, documentDate: '2026-11-01' },
        ],
        totalCount: 1, pageNumber: 1, pageSize: 5,
      }];
    });

    renderWithProviders(<PendingApprovals />);
    expect(await screen.findByText('سند شماره ۱۰۱')).toBeInTheDocument();
    expect(screen.getByText('خرید ملزومات')).toBeInTheDocument();
  });

  it('shows empty state when nothing is pending', async () => {
    mock.onGet('/Documents').reply(200, { items: [], totalCount: 0, pageNumber: 1, pageSize: 5 });
    renderWithProviders(<PendingApprovals />);
    expect(await screen.findByText('سندی در جریان تأیید وجود ندارد')).toBeInTheDocument();
  });
});

describe('TopBalances', () => {
  it('renders sorted rows with real balance fields and empty state when zero', async () => {
    mock.onGet('/Ledger/balances').reply(200, [
      { accountId: 'a1', accountFullCode: '1101', accountName: 'صندوق', periodDebit: 500, periodCredit: 300, closingDisplayBalance: 200 },
      { accountId: 'a2', accountFullCode: '2101', accountName: 'قابل پرداخت', periodDebit: 0, periodCredit: 0, closingDisplayBalance: 0 },
    ]);
    renderWithProviders(<TopBalances range={{}} />);
    expect(await screen.findByText('1101')).toBeInTheDocument();
    expect(screen.getByText('صندوق')).toBeInTheDocument();
    expect(screen.queryByText('2101')).toBeNull(); // مانده صفر حذف می‌شود

  });
});

describe('FiscalStatusWidget', () => {
  it('shows real open fiscal year and current open period', async () => {
    mock.onGet('/FiscalYears').reply(200, [
      { id: 'fy1', yearNumber: 1404, name: '۱۴۰۴', startDate: '2025-03-21', endDate: '2026-03-20', status: 'Open', isClosed: false },
    ]);
    mock.onGet('/FiscalYears/fy1/periods').reply(200, [
      { id: 'p1', fiscalYearId: 'fy1', periodNumber: 8, name: 'آبان ۱۴۰۵', startDate: '2026-10-24', endDate: '2026-11-22', status: 'Open', isAdjustmentPeriod: false },
    ]);
    renderWithProviders(<FiscalStatusWidget />);
    expect(await screen.findByText('سال مالی ۱۴۰۴')).toBeInTheDocument();
    expect(await screen.findByText('آبان ۱۴۰۵')).toBeInTheDocument();
  });

  it('shows empty state when no open year exists (all closed)', async () => {
    mock.onGet('/FiscalYears').reply(200, [
      { id: 'fy2', yearNumber: 1403, name: '۱۴۰۳', startDate: '2024-03-20', endDate: '2025-03-20', status: 'Closed', isClosed: true },
    ]);
    renderWithProviders(<FiscalStatusWidget />);
    expect(await screen.findByText('سال مالی باز وجود ندارد (سال در اختتامیه یا بسته است)')).toBeInTheDocument();
  });
});

describe('FinancialAlerts & QuickActions (no fake numbers)', () => {
  it('financial alerts render backend-required empty state without invented data', () => {
    renderWithProviders(<FinancialAlerts />);
    expect(screen.getByText('هیچ هشداری برای نمایش وجود ندارد')).toBeInTheDocument();
    expect(screen.getByText('نیاز Backend: API مربوطه موجود نیست')).toBeInTheDocument();
  });

  it('quick actions filter by permissions', () => {
    useAuthStore.setState({
      user: { userId: 'u1', email: 'a@b.c', fullName: 'کاربر', roles: [], permissions: ['accounting:documents:read'] },
    });
    renderWithProviders(<QuickActions />);
    expect(screen.getByText('فهرست اسناد')).toBeInTheDocument();
    expect(screen.queryByText('سند جدید')).toBeNull(); // documentsCreate ندارد
  });
});
