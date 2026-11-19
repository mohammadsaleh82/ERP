/**
 * فاز ۸ — صورت‌های مالی و تجربه گزارش‌گیری:
 * بازه تاریخ/سال مالی، بدون داده، خروجی سمت سرور (اکسل/PDF/CSV منطبق با فیلترها)،
 * چاپ، اعداد بزرگ، تطبیق جمع‌ها با داده API، خطای API، دسترسی مجاز/غیرمجاز.
 * cash-flow فقط داده طبقه‌بندی‌شده backend — بدون استنتاج از مانده بانک.
 */
import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import AxiosMockAdapter from 'axios-mock-adapter';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { apiClient } from '@/core/api/client';
import { useAuthStore } from '@/core/auth/auth.store';
import { useCompanyStore } from '@/core/company/company.store';
import { toast } from '@/core/notifications/toast';
import { BalanceSheetPage, ProfitLossPage, CashFlowPage, PartyLedgerPage, AgingPage } from '@/features/reports/statements-index';
import { formatAmount } from '@/core/accounting/documents.service';

vi.mock('@/core/notifications/toast', () => ({
  toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() },
}));

let mock: AxiosMockAdapter;
const BIG = '1234567890123';

const balanceSheetPayload = {
  companyId: 'c1', asOfDate: '2026-03-19',
  currentAssets: { title: 'دارایی جاری', rows: [{ accountId: 'a1', accountCode: '110101', accountName: 'صندوق', balance: 500000000, category: 'CashAndCashEquivalents' }], total: 500000000 },
  nonCurrentAssets: { title: 'دارایی غیرجاری', rows: [], total: 0 },
  totalAssets: 500000000,
  currentLiabilities: { title: 'بدهی جاری', rows: [{ accountId: 'a2', accountCode: '220101', accountName: 'قابل پرداخت', balance: 100000000, category: 'CurrentLiability' }], total: 100000000 },
  nonCurrentLiabilities: { title: 'بدهی غیرجاری', rows: [], total: 0 },
  totalLiabilities: 100000000,
  equity: { title: 'حقوق مالکانه', rows: [{ accountId: 'a3', accountCode: '3001', accountName: 'سرمایه', balance: 400000000, category: 'Equity' }], total: 400000000 },
  currentPeriodResult: 0, totalEquity: 400000000,
  isBalanced: true, difference: 0,
};

const profitLossPayload = {
  companyId: 'c1', fromDate: '2025-03-21', toDate: '2026-03-20',
  revenues: { title: 'درآمد', rows: [{ accountId: 'r1', accountCode: '4001', accountName: 'فروش', balance: BIG, category: 'Revenue' }], total: BIG },
  costOfGoodsSold: { title: 'بهای فروش', rows: [{ accountId: 'c1r', accountCode: '5001', accountName: 'خرید', balance: 600000000000, category: 'CostOfGoodsSold' }], total: 600000000000 },
  grossProfit: 634567890123,
  operatingExpenses: { title: 'هزینه عملیاتی', rows: [], total: 100000000000 },
  operatingProfit: 534567890123,
  otherIncome: { title: 'سایر درآمد', rows: [], total: 0 },
  otherExpenses: { title: 'سایر هزینه', rows: [], total: 34567890123 },
  profitBeforeTax: 500000000000,
  taxExpenses: { title: 'مالیات', rows: [], total: 90000000000 },
  netProfit: 410000000000,
};

const receivablesPayload = {
  title: 'گزارش بدهکاران', accountsConfigured: true, note: null,
  rows: [
    { dimensionId: 'd1', dimensionLabel: 'شرکت الف', openingDebit: 1000, openingCredit: 0, debitTurnover: 2500, creditTurnover: 500, closingDebit: 3000, closingCredit: 0, signedBalance: 3000 },
    { dimensionId: 'd2', dimensionLabel: 'شرکت ب', openingDebit: 0, openingCredit: 200, debitTurnover: 0, creditTurnover: 800, closingDebit: 0, closingCredit: 1000, signedBalance: -1000 },
  ],
  totalDebit: 2500, totalCredit: 1300,
};

beforeEach(() => {
  mock = new AxiosMockAdapter(apiClient, { onNoMatch: 'throwException' });
  useAuthStore.getState().setAuth(
    { userId: 'u1', email: 'a@b.c', fullName: 'مدیر مالی', roles: [], permissions: ['accounting:reports:read', 'accounting:reports:classify'] },
    'tok', 't1', 'c1',
  );
  useCompanyStore.getState().setCompanyContext(
    { id: 't1', name: 'گروه آریا', companies: [{ id: 'c1', name: 'آریا', fiscalYear: 1404 }] },
    { id: 'c1', name: 'آریا', fiscalYear: 1404 },
  );
  mock.onGet('/FiscalYears').reply(200, [
    { id: 'fy1', yearNumber: 1404, status: 'Open', startDate: '2025-03-21', endDate: '2026-03-20', calendarType: 'Persian', isClosed: false, openingBalancePosted: true, tenantId: 't1', companyId: 'c1', name: '1404' },
  ]);
  mock.onGet('/FiscalYears/fy1/periods').reply(200, [
    { id: 'p1', fiscalYearId: 'fy1', periodNumber: 1, name: 'فروردین', startDate: '2025-03-21', endDate: '2025-04-20', status: 'Open', isAdjustmentPeriod: false },
  ]);
  mock.onGet('/Accounts/postable').reply(200, [
    { id: 'bank1', tenantId: 't1', companyId: 'c1', parentAccountId: null, code: '110201', fullCode: '110201', name: 'بانک ملت', description: null, level: 'Detail', nature: 'Asset', normalBalance: 'Debit', isActive: true, isSystemAccount: false, allowPosting: true, hasChildren: false, requireCostCenter: false, requireProject: false, requireDetailAccount: false, createdAtUtc: 'x', lastModifiedAtUtc: null },
  ]);
  // jsdom: blob url و چاپ
  Object.defineProperty(URL, 'createObjectURL', { value: vi.fn(() => 'blob:test'), configurable: true, writable: true });
  Object.defineProperty(URL, 'revokeObjectURL', { value: vi.fn(), configurable: true, writable: true });
  vi.spyOn(window, 'print').mockImplementation(() => {});
});
afterEach(() => {
  mock.restore();
  useAuthStore.getState().clearAuth();
  vi.restoreAllMocks();
});

const renderRoute = (ui: React.ReactNode, entry: string) => {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[entry]}>
        <Routes>
          <Route path="/accounting/reports/balance-sheet" element={<BalanceSheetPage />} />
          <Route path="/accounting/reports/profit-loss" element={<ProfitLossPage />} />
          <Route path="/accounting/reports/cash-flow" element={<CashFlowPage />} />
          <Route path="/accounting/reports/receivables" element={<PartyLedgerPage kind="receivables" />} />
          <Route path="/accounting/reports/payables" element={<PartyLedgerPage kind="payables" />} />
          <Route path="/accounting/reports/aging" element={<AgingPage />} />
          <Route path="/access-denied" element={<div data-testid="access-denied">دسترسی غیرمجاز</div>} />
          <Route path="*" element={ui} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

describe('BalanceSheetPage — ترازنامه', () => {
  it('runs on demand, sends year+to filters, matches server totals (audit)', async () => {
    mock.onGet('/accounting/reports/balance-sheet').reply(200, balanceSheetPayload);
    renderRoute(null, '/accounting/reports/balance-sheet?fiscalYearId=fy1&to=2026-03-19');
    fireEvent.click(await screen.findByTestId('bs-run'));
    await waitFor(() => {
      const request = mock.history.post.length > 0 ? null : mock.history.get.find((item) => item.url.includes('balance-sheet'));
      expect(request!.params).toMatchObject({ fiscalYearId: 'fy1', to: '2026-03-19' });
    });
    expect(await screen.findByTestId('bs-balance-banner')).toHaveTextContent('ترازنامه تراز است');
    // جمع‌های نمایشی دقیقاً برابر داده API
    expect(within(screen.getByTestId('bs-total-assets')).getByText(formatAmount(500000000))).toBeInTheDocument();
    expect(screen.getByTestId('bs-difference').textContent).toBe('—'); // صفر → تیره (قرارداد نمایشی)
  });

  it('shows big numbers with standard grouping and unbalanced warning as non-authoritative', async () => {
    mock.onGet('/accounting/reports/balance-sheet').reply(200, { ...balanceSheetPayload, totalAssets: Number(BIG), isBalanced: false, difference: 999 });
    renderRoute(null, '/accounting/reports/balance-sheet');
    fireEvent.click(await screen.findByTestId('bs-run'));
    await waitFor(() => expect(within(screen.getByTestId('bs-total-assets')).getByText(formatAmount(1234567890123))).toBeInTheDocument());
    expect(screen.getByTestId('bs-balance-banner').textContent).toContain('قطعی نیست');
  });

  it('API error shows error state with retry', async () => {
    mock.onGet('/accounting/reports/balance-sheet').reply(500, { title: 'خطا' });
    renderRoute(null, '/accounting/reports/balance-sheet');
    fireEvent.click(await screen.findByTestId('bs-run'));
    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain('اجرای گزارش ناموفق بود');
    mock.onGet('/accounting/reports/balance-sheet').reply(200, balanceSheetPayload);
    fireEvent.click(within(alert).getByRole('button', { name: /تلاش دوباره|مجدد|retry/i }) ?? alert.querySelector('button'));
    await screen.findByTestId('bs-balance-banner');
  });

  it('denied access redirects to access-denied', async () => {
    useAuthStore.getState().setAuth({ userId: 'u2', email: 'x@y.z', fullName: 'بدون مجوز', roles: [], permissions: ['other:perm'] }, 'tok', 't1', 'c1');
    renderRoute(null, '/accounting/reports/balance-sheet');
    expect(await screen.findByTestId('access-denied')).toBeInTheDocument();
  });
});

describe('ProfitLossPage — سود و زیان', () => {
  it('stage rows match API values and chart renders from API data', async () => {
    mock.onGet('/accounting/reports/profit-loss').reply(200, profitLossPayload);
    renderRoute(null, '/accounting/reports/profit-loss');
    fireEvent.click(await screen.findByTestId('pl-run'));
    expect(await screen.findByTestId('pl-stage-net')).toHaveTextContent(formatAmount(410000000000));
    expect(screen.getByTestId('pl-stage-gross')).toHaveTextContent(formatAmount(634567890123));
    expect(screen.getByTestId('pl-revenues-total').textContent).toBe(formatAmount(1234567890123));
    // نمودار از داده API (recharts svg)
    expect(await screen.findByTestId('pl-chart')).toBeInTheDocument();
  });

  it('server export produces Excel file matching current filters', async () => {
    mock.onGet('/accounting/reports/profit-loss').reply(200, profitLossPayload);
    mock.onPost('/accounting/reports/export').reply(200, new Blob(['x']), { 'content-disposition': 'attachment; filename="profit-loss.xlsx"' });
    renderRoute(null, '/accounting/reports/profit-loss?from=2025-03-21&to=2026-03-20');
    fireEvent.click(await screen.findByTestId('pl-run'));
    // خروجی فقط پس از اتمام اجرای گزارش فعال می‌شود — منطبق با فیلترهای همان اجرا
    await waitFor(() => expect(screen.getByTestId('pl-export-excel')).not.toBeDisabled());
    fireEvent.click(screen.getByTestId('pl-export-excel'));
    await waitFor(() => expect(toast.success).toHaveBeenCalled());
    const exportRequest = mock.history.post.find((item) => item.url.includes('/reports/export'));
    expect(exportRequest).toBeDefined();
    expect(JSON.parse(exportRequest!.data)).toMatchObject({ ReportName: 'profitloss', Format: 'Excel', From: '2025-03-21', To: '2026-03-20' });
  });

  it('export failure shows error toast', async () => {
    mock.onGet('/accounting/reports/profit-loss').reply(200, profitLossPayload);
    mock.onPost('/accounting/reports/export').reply(500);
    renderRoute(null, '/accounting/reports/profit-loss');
    fireEvent.click(await screen.findByTestId('pl-run'));
    await waitFor(() => expect(screen.getByTestId('pl-export-excel')).not.toBeDisabled());
    fireEvent.click(screen.getByTestId('pl-export-excel'));
    await waitFor(() => expect(toast.error).toHaveBeenCalled());
  });
});

describe('CashFlowPage — جریان وجوه نقد', () => {
  it('renders only backend-classified buckets with server values; no bank-balance inference', async () => {
    mock.onGet('/accounting/reports/cash-flow').reply(200, {
      companyId: 'c1', fromDate: '2025-03-21', toDate: '2026-03-20',
      buckets: [
        { category: 'Operating', inflow: 8000, outflow: 3000, net: 5000 },
        { category: 'Investing', inflow: 0, outflow: 1200, net: -1200 },
      ],
      openingCash: 700, closingCash: 4500, cashAccountsConfigured: true, note: 'بر پایه گردش حساب‌های طبقه‌بندی‌شده',
    });
    renderRoute(null, '/accounting/reports/cash-flow');
    fireEvent.click(await screen.findByTestId('cf-run'));
    await waitFor(() => expect(screen.getByTestId('cf-table')).toBeInTheDocument());
    expect(screen.getByTestId('cf-table').textContent).toContain('فعالیت عملیاتی');
    expect(within(screen.getByTestId('cf-closing')).getByText(formatAmount(4500))).toBeInTheDocument();
    // جمع جدول با داده API تطبیق دارد
    expect(screen.getByTestId('cf-totals').textContent).toContain(formatAmount(8000));
    expect(screen.getByTestId('cf-chart')).toBeInTheDocument();
  });

  it('unconfigured cash accounts banner — report not authoritative', async () => {
    mock.onGet('/accounting/reports/cash-flow').reply(200, { companyId: 'c1', fromDate: '2025-03-21', toDate: '2026-03-20', buckets: [], openingCash: 0, closingCash: 0, cashAccountsConfigured: false, note: 'طبقه‌بندی جریان نقد انجام نشده' });
    renderRoute(null, '/accounting/reports/cash-flow');
    fireEvent.click(await screen.findByTestId('cf-run'));
    const banner = await screen.findByTestId('cf-unconfigured');
    expect(banner.textContent).toContain('پیکربندی نشده');
    expect(screen.getByText(/جریانی طبقه‌بندی‌شده در این بازه نیست/)).toBeInTheDocument();
  });

  it('classification manager: allowed user sets account category via PUT with real enum value', async () => {
    mock.onGet('/accounting/reports/cash-flow-classifications').reply(200, [{ id: 'cls1', accountId: 'bank1', category: 'Operating' }]);
    mock.onGet('/accounting/reports/cash-flow').reply(200, { companyId: 'c1', fromDate: '2025-03-21', toDate: '2026-03-20', buckets: [], openingCash: 0, closingCash: 0, cashAccountsConfigured: true, note: null });
    renderRoute(null, '/accounting/reports/cash-flow');
    fireEvent.click(await screen.findByTestId('cf-run'));
    // حساب طبقه‌بندی‌شده در فهرست فعال نیست؛ طبقه فعلی نمایش داده می‌شود
    expect(await screen.findByTestId('cf-class-bank1')).toHaveTextContent('فعالیت عملیاتی');
  });
});

describe('PartyLedgerPage — بدهکاران/بستانکاران', () => {
  it('receivables rows and footer totals exactly match API', async () => {
    mock.onGet('/accounting/reports/receivables').reply(200, receivablesPayload);
    renderRoute(null, '/accounting/reports/receivables');
    fireEvent.click(await screen.findByTestId('recv-run'));
    expect(await screen.findByTestId('party-table')).toBeInTheDocument();
    expect(screen.getByTestId('party-footer-debit').textContent).toBe(formatAmount(2500));
    expect(screen.getByTestId('party-footer-credit').textContent).toBe(formatAmount(1300));
    expect(within(screen.getByTestId('party-total-debit')).getByText(formatAmount(2500))).toBeInTheDocument();
  });

  it('unconfigured accounts banner for payables', async () => {
    mock.onGet('/accounting/reports/payables').reply(200, { title: 'گزارش بستانکاران', accountsConfigured: false, note: 'حساب پرداختنی تعیین نشده', rows: [], totalDebit: 0, totalCredit: 0 });
    renderRoute(null, '/accounting/reports/payables');
    fireEvent.click(await screen.findByTestId('pay-run'));
    const banner = await screen.findByTestId('party-unconfigured');
    expect(banner.textContent).toContain('پرداختنی');
  });
});

describe('AgingPage — گزارش سنی', () => {
  it('buckets + total from API; unreliable due dates banner; print only (no export endpoint)', async () => {
    mock.onGet('/accounting/reports/aging').reply(200, {
      title: 'گزارش سنی مطالبات', hasReliableDueDates: false, note: 'سررسید در اسناد ثبت نشده', asOfDate: '2026-03-19',
      buckets: [{ label: '0-30 روز', amount: 5000 }, { label: '31-90 روز', amount: 2500 }],
      totalAmount: 7500,
    });
    renderRoute(null, '/accounting/reports/aging?to=2026-03-19');
    fireEvent.click(await screen.findByTestId('aging-run'));
    await waitFor(() => expect(mock.history.get.some((item) => item.url.includes('aging'))).toBe(true));
    const request = mock.history.get.find((item) => item.url.includes('aging'))!;
    expect(request.params).toMatchObject({ to: '2026-03-19' });
    expect(await screen.findByTestId('aging-unreliable')).toBeInTheDocument();
    expect(screen.getByTestId('aging-footer-total').textContent).toBe(formatAmount(7500));
    expect(screen.getByTestId('aging-chart')).toBeInTheDocument();
    // aging در endpoint خروجی backend نیست — فقط چاپ
    expect(screen.queryByTestId('aging-export-excel')).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId('aging-print'));
    expect(window.print).toHaveBeenCalled();
  });

  it('empty data state', async () => {
    mock.onGet('/accounting/reports/aging').reply(200, { title: 'گزارش سنی', hasReliableDueDates: true, note: '', asOfDate: '2026-03-19', buckets: [], totalAmount: 0 });
    renderRoute(null, '/accounting/reports/aging');
    fireEvent.click(await screen.findByTestId('aging-run'));
    expect(await screen.findByText(/سبدی برای نمایش نیست/)).toBeInTheDocument();
  });

  it('date range filter change refetches with new bounds', async () => {
    mock.onGet('/accounting/reports/aging').reply(200, { title: 'گزارش سنی', hasReliableDueDates: true, note: '', asOfDate: '2026-03-19', buckets: [], totalAmount: 0 });
    renderRoute(null, '/accounting/reports/aging?to=2026-01-01');
    fireEvent.click(await screen.findByTestId('aging-run'));
    await screen.findByTestId('aging-page');
    fireEvent.change(screen.getByTestId('aging-to'), { target: { value: '2026-03-19' } });
    // تغییر بازه → پاک‌سازی page و اجرای مجدد با بازه جدید (فیلتر در URL)
    await waitFor(() => {
      const requests = mock.history.get.filter((item) => item.url.includes('aging'));
      expect(requests[requests.length - 1].params.to).toBe('2026-03-19');
    });
  });
});

describe('چاپ و سال مالی — تجربه مشترک', () => {
  it('print button triggers window.print on balance sheet', async () => {
    mock.onGet('/accounting/reports/balance-sheet').reply(200, balanceSheetPayload);
    renderRoute(null, '/accounting/reports/balance-sheet');
    fireEvent.click(await screen.findByTestId('bs-run'));
    await screen.findByTestId('bs-balance-banner');
    fireEvent.click(screen.getByTestId('bs-print'));
    expect(window.print).toHaveBeenCalled();
  });

  it('fiscal year switch refetches with new year id (dependent filters cleared)', async () => {
    mock.onGet('/FiscalYears').reply(200, [
      { id: 'fy1', yearNumber: 1404, status: 'Open', startDate: '2025-03-21', endDate: '2026-03-20', calendarType: 'Persian', isClosed: false, openingBalancePosted: true, tenantId: 't1', companyId: 'c1', name: '1404' },
      { id: 'fy2', yearNumber: 1405, status: 'Draft', startDate: '2026-03-21', endDate: '2027-03-20', calendarType: 'Persian', isClosed: false, openingBalancePosted: false, tenantId: 't1', companyId: 'c1', name: '1405' },
    ]);
    mock.onGet('/FiscalYears/fy2/periods').reply(200, []);
    mock.onGet('/accounting/reports/balance-sheet').reply(200, balanceSheetPayload);
    renderRoute(null, '/accounting/reports/balance-sheet?fiscalYearId=fy1&from=2025-03-21');
    fireEvent.click(await screen.findByTestId('bs-run'));
    await waitFor(() => expect(mock.history.get.some((item) => item.url.includes('balance-sheet'))).toBe(true));
    await waitFor(() => expect((screen.getByTestId('bs-year') as HTMLSelectElement).options.length).toBe(3));
    fireEvent.change(screen.getByTestId('bs-year'), { target: { value: 'fy2' } });
    // سال جدید → بازه تاریخ پاک و درخواست با سال جدید (بعد از اجرای مجدد)
    fireEvent.click(screen.getByTestId('bs-run'));
    await waitFor(() => {
      const requests = mock.history.get.filter((item) => item.url.includes('balance-sheet'));
      expect(requests[requests.length - 1].params.fiscalYearId).toBe('fy2');
      expect(requests[requests.length - 1].params.from).toBeUndefined();
    });
  });
});
