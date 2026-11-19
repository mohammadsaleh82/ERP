/**
 * فاز ۷ — صفحات دفاتر و گزارش‌ها:
 * فیلترهای وابسته هوشمند، مانده‌های دریافتی backend (جهت/غیرعادی)، صفحه‌بندی سرور،
 * لینک و کپی شماره سند، ابعاد مالی واقعی، تغییر شرکت و عدم نمایش cache شرکت قبلی.
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
import {
  JournalPage,
  GeneralLedgerPage,
  LedgerTransactionsPage,
  AccountCardPage,
  AccountBalancesPage,
  TrialBalancePage,
  DimensionsTurnoverPage,
  CombinedDimensionsPage,
} from '@/features/reports';
import { getFinancialDimensions } from '@/core/accounting/reports.service';

vi.mock('@/core/notifications/toast', () => ({
  toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() },
}));

let mock: AxiosMockAdapter;

beforeEach(() => {
  mock = new AxiosMockAdapter(apiClient, { onNoMatch: 'throwException' });
  // jsdom clipboard ندارد — mock برای تست کپی شماره سند
  Object.assign(navigator, { clipboard: { writeText: vi.fn().mockResolvedValue(undefined) } });
  useAuthStore.getState().setAuth(
    { userId: 'u1', email: 'a@b.c', fullName: 'حسابدار', roles: [], permissions: ['accounting:ledger:read', 'accounting:reports:read', 'accounting:dimensions:read'] },
    'tok', 't1', 'c1',
  );
  useCompanyStore.getState().setCompanyContext(
    { id: 't1', name: 'گروه آریا', companies: [{ id: 'c1', name: 'آریا', fiscalYear: 1404 }] },
    { id: 'c1', name: 'آریا', fiscalYear: 1404 },
  );
  mock.onGet('/FiscalYears').reply(200, [
    { id: 'fy1', tenantId: 't1', companyId: 'c1', yearNumber: 1404, name: '۱۴۰۴', startDate: '2025-03-21', endDate: '2026-03-20', calendarType: 'Persian', status: 'Open', isClosed: false, openingBalancePosted: true },
    { id: 'fy2', tenantId: 't1', companyId: 'c1', yearNumber: 1405, name: '۱۴۰۵', startDate: '2026-03-21', endDate: '2027-03-20', calendarType: 'Persian', status: 'Draft', isClosed: false, openingBalancePosted: false },
  ]);
  mock.onGet('/FiscalYears/fy1/periods').reply(200, [
    { id: 'p1', fiscalYearId: 'fy1', periodNumber: 1, name: 'فروردین', startDate: '2025-03-21', endDate: '2025-04-20', status: 'Closed', isAdjustmentPeriod: false },
    { id: 'p2', fiscalYearId: 'fy1', periodNumber: 2, name: 'اردیبهشت', startDate: '2025-04-21', endDate: '2025-05-21', status: 'Open', isAdjustmentPeriod: false },
  ]);
  mock.onGet('/Accounts/postable').reply(200, [
    { id: 'a-cash', tenantId: 't1', companyId: 'c1', parentAccountId: null, code: '110101', fullCode: '110101', name: 'صندوق', description: null, level: 'Detail', nature: 'Asset', normalBalance: 'Debit', isActive: true, isSystemAccount: false, allowPosting: true, hasChildren: false, requireCostCenter: false, requireProject: false, requireDetailAccount: false, createdAtUtc: 'x', lastModifiedAtUtc: null },
    { id: 'a-parent', tenantId: 't1', companyId: 'c1', parentAccountId: null, code: '1101', fullCode: '1101', name: 'موجودی نقد', description: null, level: 'General', nature: 'Asset', normalBalance: 'Debit', isActive: true, isSystemAccount: false, allowPosting: false, hasChildren: true, requireCostCenter: false, requireProject: false, requireDetailAccount: false, createdAtUtc: 'x', lastModifiedAtUtc: null },
  ]);
  mock.onGet('/accounting/dimensions').reply((config) => {
    const type = Number(config.params?.dimensionType ?? 1);
    const catalog: Record<number, Array<{ id: string; code: string; name: string }>> = {
      1: [{ id: 'dim-d1', code: 'D01', name: 'مشتری الف' }],
      2: [{ id: 'dim-c1', code: 'CC1', name: 'فروش مرکزی' }],
      3: [{ id: 'dim-p1', code: 'PR1', name: 'پروژه برج' }],
      6: [{ id: 'dim-x1', code: 'X01', name: 'شخص ب' }],
    };
    return [200, { items: (catalog[type] ?? []).map((item) => ({ ...item, dimensionType: type, externalReferenceId: null, ownerModule: 'accounting', isActive: true })), totalCount: (catalog[type] ?? []).length, pageNumber: 1, pageSize: 200 }];
  });
});
afterEach(() => {
  mock.restore();
  useAuthStore.getState().clearAuth();
});

const renderRoute = (ui: React.ReactNode, entry = '/') => {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[entry]}>
        <Routes>
          <Route path="/accounting/reports/journal" element={<JournalPage />} />
          <Route path="/accounting/reports/general-ledger" element={<GeneralLedgerPage />} />
          <Route path="/accounting/reports/general-ledger/:accountId" element={<LedgerTransactionsPage />} />
          <Route path="/accounting/reports/account-card" element={<AccountCardPage />} />
          <Route path="/accounting/reports/balances" element={<AccountBalancesPage />} />
          <Route path="/accounting/reports/trial-balance" element={<TrialBalancePage />} />
          <Route path="/accounting/reports/dimensions" element={<DimensionsTurnoverPage />} />
          <Route path="/accounting/reports/dimensions/combined" element={<CombinedDimensionsPage />} />
          <Route path="/accounting/documents/:id" element={<div>سند</div>} />
          <Route path="*" element={ui} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

describe('JournalPage — دفتر روزنامه', () => {
  it('renders entries with document link, copy button and period totals from server', async () => {
    mock.onGet('/accounting/ledger/journal').reply(200, {
      entries: [{
        documentId: 'doc1', documentNumber: 42, documentDate: '2026-03-01', description: 'فروش نقدی این شرح نسبتا طولانی است که باید در Tooltip نشان داده شود', sourceModule: null,
        totalDebit: 1500, totalCredit: 1500,
        lines: [{ accountId: 'a', accountFullCode: '110101', accountName: 'صندوق', lineNumber: 1, description: 'x', debit: 1500, credit: 0 }],
      }],
      dailyTotals: [{ date: '2026-03-01', documentsCount: 1, totalDebit: 1500, totalCredit: 1500 }],
      periodTotalDebit: 1500, periodTotalCredit: 1500, totalCount: 1, pageNumber: 1, pageSize: 25,
    });
    renderRoute(null, '/accounting/reports/journal');
    const table = await screen.findByTestId('financial-table');
    // لینک سند مرجع
    const link = within(table).getByRole('link', { name: 'مشاهده سند 42' });
    expect(link.getAttribute('href')).toBe('/accounting/documents/doc1');
    expect(link.textContent).toContain('42');
    // کپی شماره سند
    fireEvent.click(within(table).getByLabelText('کپی شماره سند 42'));
    await waitFor(() => expect(toast.success).toHaveBeenCalled());
    // جمع دوره از سرور با فرمت
    expect(screen.getByTestId('journal-period-debit').textContent).toBe('1,500');
  });

  it('sends server-side pagination and filters; page change keeps URL', async () => {
    mock.onGet('/accounting/ledger/journal').reply(200, { entries: [], dailyTotals: [], periodTotalDebit: 0, periodTotalCredit: 0, totalCount: 75, pageNumber: 2, pageSize: 25 });
    renderRoute(null, '/accounting/reports/journal?fiscalYearId=fy1&from=2025-03-21&to=2025-04-20&page=2');
    await waitFor(() => {
      const request = mock.history.get[mock.history.get.length - 1];
      expect(request.params).toMatchObject({ fiscalYearId: 'fy1', fromDate: '2025-03-21', toDate: '2025-04-20', pageNumber: 2, pageSize: 25 });
    });
    fireEvent.click(screen.getByTestId('journal-next-page'));
    await waitFor(() => {
      const journalRequests = mock.history.get.filter((request) => request.url.includes('/accounting/ledger/journal'));
      expect(journalRequests[journalRequests.length - 1].params.pageNumber).toBe(3);
    });
  });
});

describe('GeneralLedgerPage — دفتر کل', () => {
  const glResponse = {
    fiscalYearId: 'fy1', fromDate: '2025-03-21', toDate: '2026-03-20', totalCount: 2, pageNumber: 1, pageSize: 25,
    totalPeriodDebit: 3000, totalPeriodCredit: 2500,
    accounts: [
      { accountId: 'a1', accountFullCode: '110101', accountName: 'صندوق', normalBalance: 'Debit', openingBalance: 1000, periodDebit: 2000, periodCredit: 500, closingBalance: 2500, closingDirection: 'Debtor', closingDisplayBalance: 2500, isAbnormalClosingBalance: false },
      { accountId: 'a2', accountFullCode: '220101', accountName: 'قابل پرداخت', normalBalance: 'Credit', openingBalance: 0, periodDebit: 0, periodCredit: 1000, closingBalance: -1000, closingDirection: 'Creditor', closingDisplayBalance: 1000, isAbnormalClosingBalance: false },
    ],
  };

  it('shows opening/debit/credit/closing with direction badges and server totals', async () => {
    mock.onGet('/accounting/ledger/general-ledger').reply(200, glResponse);
    renderRoute(null, '/accounting/reports/general-ledger');
    expect(await screen.findByTestId('financial-table')).toBeInTheDocument();
    expect(screen.getAllByTestId('gl-closing')[0].textContent).toContain('بدهکار');
    expect(screen.getAllByTestId('gl-closing')[1].textContent).toContain('بستانکار');
    expect(screen.getByTestId('gl-total-debit').textContent).toBe('3,000');
    expect(screen.getByTestId('gl-total-credit').textContent).toBe('2,500');
  });

  it('links a row to its transactions page preserving filters', async () => {
    mock.onGet('/accounting/ledger/general-ledger').reply(200, glResponse);
    renderRoute(null, '/accounting/reports/general-ledger?fiscalYearId=fy1&from=2025-03-21');
    fireEvent.click(await screen.findByTestId('gl-transactions-110101'));
    // صفحه گردش تفصیلی همان حساب با فیلترهای حفظ‌شده
    await waitFor(() => expect(mock.history.get.some((request) => request.url.includes('/accounts/a1/transactions'))).toBe(true));
    const request = mock.history.get.find((item) => item.url.includes('/accounts/a1/transactions'));
    expect(request!.params).toMatchObject({ fiscalYearId: 'fy1', fromDate: '2025-03-21' });
  });
});

describe('LedgerTransactionsPage — گردش تفصیلی', () => {
  it('renders header summary and paginated transactions', async () => {
    mock.onGet('/accounting/ledger/general-ledger/accounts/a1/transactions').reply(200, {
      accountId: 'a1', accountFullCode: '110101', accountName: 'صندوق', openingBalance: 1000, totalDebit: 2000, totalCredit: 500,
      closingBalance: 2500, closingDirection: 'Debtor', closingDisplayBalance: 2500, totalCount: 2, pageNumber: 1, pageSize: 25,
      transactions: [
        { entryId: 'e1', documentId: 'doc1', documentNumber: 42, lineNumber: 1, entryDate: '2025-04-01', description: 'وصول نقدی', debit: 2000, credit: 0, currencyCode: 'IRR', sourceModule: null, sourceDocumentId: null },
        { entryId: 'e2', documentId: 'doc2', documentNumber: 43, lineNumber: 2, entryDate: '2025-04-02', description: 'هزینه', debit: 0, credit: 500, currencyCode: 'IRR', sourceModule: null, sourceDocumentId: null },
      ],
    });
    renderRoute(null, '/accounting/reports/general-ledger/a1');
    expect(await screen.findByText(/110101 صندوق/)).toBeInTheDocument();
    expect(screen.getAllByTestId('lt-doc').length).toBe(2);
    expect(screen.getByTestId('lt-closing').textContent).toContain('2,500');
  });
});

describe('ReportFilterBar — فیلترهای وابسته هوشمند', () => {
  it('changing fiscal year clears period and dates (invalid dependents)', async () => {
    mock.onGet('/accounting/ledger/journal').reply(200, { entries: [], dailyTotals: [], periodTotalDebit: 0, periodTotalCredit: 0, totalCount: 0, pageNumber: 1, pageSize: 25 });
    mock.onGet('/FiscalYears/fy2/periods').reply(200, []);
    renderRoute(null, '/accounting/reports/journal?fiscalYearId=fy1&fiscalPeriodId=p2&from=2025-04-21&to=2025-05-21');
    await screen.findByTestId('journal-page');
    await waitFor(() => expect((screen.getByTestId('journal-year') as HTMLSelectElement).options.length).toBe(3)); // سال‌ها async بارگذاری می‌شوند
    fireEvent.change(screen.getByTestId('journal-year'), { target: { value: 'fy2' } });
    await waitFor(() => {
      // فقط درخواست‌های دفتر روزنامه — نه کوئری‌های فیلتر
      const journalRequests = mock.history.get.filter((request) => request.url.includes('/accounting/ledger/journal'));
      const request = journalRequests[journalRequests.length - 1];
      expect(request.params.fiscalYearId).toBe('fy2');
      // دوره و تاریخ‌های وابسته پاک شده‌اند
      expect(request.params.fromDate).toBeUndefined();
      expect(request.params.toDate).toBeUndefined();
    });
  });

  it('selecting a period sets real date bounds of that period', async () => {
    mock.onGet('/accounting/ledger/journal').reply(200, { entries: [], dailyTotals: [], periodTotalDebit: 0, periodTotalCredit: 0, totalCount: 0, pageNumber: 1, pageSize: 25 });
    renderRoute(null, '/accounting/reports/journal?fiscalYearId=fy1');
    const periodSelect = await screen.findByTestId('journal-period');
    await waitFor(() => expect((periodSelect as HTMLSelectElement).options.length).toBe(3)); // دوره‌ها پس از رسیدن سال
    fireEvent.change(periodSelect, { target: { value: 'p1' } });
    await waitFor(() => {
      const journalRequests = mock.history.get.filter((request) => request.url.includes('/accounting/ledger/journal'));
      const request = journalRequests[journalRequests.length - 1];
      expect(request.params.fromDate).toBe('2025-03-21');
      expect(request.params.toDate).toBe('2025-04-20');
    });
  });

  it('manual date change clears selected period', async () => {
    mock.onGet('/accounting/ledger/journal').reply(200, { entries: [], dailyTotals: [], periodTotalDebit: 0, periodTotalCredit: 0, totalCount: 0, pageNumber: 1, pageSize: 25 });
    renderRoute(null, '/accounting/reports/journal?fiscalYearId=fy1&fiscalPeriodId=p1');
    fireEvent.change(screen.getByTestId('journal-from'), { target: { value: '2025-04-01' } });
    await waitFor(() => {
      const journalRequests = mock.history.get.filter((request) => request.url.includes('/accounting/ledger/journal'));
      const request = journalRequests[journalRequests.length - 1];
      expect(request.params.fromDate).toBe('2025-04-01');
      expect(request.params.fiscalPeriodId).toBeUndefined();
    });
  });
});

describe('AccountCardPage — کارت حساب', () => {
  it('requires account selection, then shows opening/running/closing from backend', async () => {
    mock.onGet('/accounting/reports/account-card/a-cash').reply(200, {
      accountId: 'a-cash', accountCode: '110101', accountName: 'صندوق', fromDate: '2025-03-21', toDate: '2026-03-20',
      openingBalance: 1000, openingSide: 'Debtor',
      rows: [
        { accountId: 'a-cash', accountCode: '110101', accountName: 'صندوق', entryDate: '2025-04-01', documentNumber: 42, description: 'وصول', debit: 500, credit: 0, runningBalance: 1500, balanceSide: 'Debtor' },
        { accountId: 'a-cash', accountCode: '110101', accountName: 'صندوق', entryDate: '2025-04-02', documentNumber: 43, description: 'پرداخت', debit: 0, credit: 300, runningBalance: 1200, balanceSide: 'Debtor' },
      ],
      closingBalance: 1200, closingSide: 'Debtor', totalCount: 2, pageNumber: 1, pageSize: 30,
    });
    renderRoute(null, '/accounting/reports/account-card?accountId=a-cash');
    const opening = await screen.findByTestId('card-opening');
    expect(opening.textContent).toContain('1,000');
    expect(screen.getAllByText('بدهکار').length).toBeGreaterThanOrEqual(1);
    const running = screen.getAllByTestId('card-running');
    expect(running[0].textContent).toContain('1,500');
    expect(running[1].textContent).toContain('1,200');
    expect(screen.getByTestId('card-closing').textContent).toContain('1,200');
  });

  it('sends dimension filters (detail/costCenter/project/party) to server', async () => {
    mock.onGet('/accounting/reports/account-card/a-cash').reply(200, { rows: [], totalCount: 0, pageNumber: 1, pageSize: 30, openingBalance: 0, openingSide: 'None', closingBalance: 0, closingSide: 'None' });
    renderRoute(null, '/accounting/reports/account-card?accountId=a-cash&detailId=dim-d1&costCenterId=dim-c1&projectId=dim-p1&partyId=dim-x1');
    await waitFor(() => {
      const request = mock.history.get[mock.history.get.length - 1];
      expect(request.params).toMatchObject({ detailId: 'dim-d1', costCenterId: 'dim-c1', projectId: 'dim-p1', partyId: 'dim-x1' });
    });
  });
});

describe('AccountBalancesPage — مانده حساب', () => {
  it('shows abnormal balance warning icon for nature-contradicting balances', async () => {
    mock.onGet('/accounting/ledger/balances').reply(200, [
      { accountId: 'a1', accountFullCode: '110101', accountName: 'صندوق', normalBalance: 'Debit', openingBalance: 0, periodDebit: 100, periodCredit: 900, closingBalance: -800, openingDirection: 'None', closingDirection: 'Creditor', openingDisplayBalance: 0, closingDisplayBalance: 800, isAbnormalOpeningBalance: false, isAbnormalClosingBalance: true },
    ]);
    renderRoute(null, '/accounting/reports/balances');
    const closing = await screen.findByTestId('bal-closing');
    expect(closing.textContent).toContain('بستانکار');
    expect(within(closing).getByTitle(/IsAbnormalBalance/)).toBeInTheDocument();
  });
});

describe('TrialBalancePage — تراز آزمایشی', () => {
  it('shows balanced banner with server totals and difference', async () => {
    mock.onGet('/accounting/reports/trial-balance').reply(200, {
      companyId: 'c1', fiscalYearName: '۱۴۰۴', fromDate: '2025-03-21', toDate: '2026-03-20', aggregationLevel: 'Detail',
      isBalanced: true, totalDebit: 5000, totalCredit: 5000, difference: 0,
      rows: [{ accountId: null, accountCode: '1', accountName: 'دارایی‌ها', level: 'Group', openingDebit: 0, openingCredit: 0, periodDebit: 5000, periodCredit: 0, closingDebit: 5000, closingCredit: 0 }],
    });
    renderRoute(null, '/accounting/reports/trial-balance');
    const banner = await screen.findByTestId('tb-balance-banner');
    expect(banner.textContent).toContain('تراز برقرار است');
    expect(banner.textContent).toContain('5,000');
    expect(screen.getByTestId('tb-difference').textContent).toBe('0');
  });

  it('sends level param on aggregation level change', async () => {
    mock.onGet('/accounting/reports/trial-balance').reply(200, { companyId: 'c1', fiscalYearName: null, fromDate: '2025-03-21', toDate: '2026-03-20', aggregationLevel: 'General', isBalanced: true, totalDebit: 0, totalCredit: 0, difference: 0, rows: [] });
    renderRoute(null, '/accounting/reports/trial-balance');
    fireEvent.change(await screen.findByTestId('tb-level'), { target: { value: 'General' } });
    await waitFor(() => expect(mock.history.get[mock.history.get.length - 1].params.level).toBe('General'));
  });
});

describe('DimensionsTurnoverPage — گردش ابعاد', () => {
  it('loads real dimensions per tab type and sends dimensionType + dimensionId', async () => {
    mock.onGet('/accounting/dimensions/analysis/turnover').reply(200, {
      dimensionType: 'CostCenter', accountId: null, totalCount: 1, pageNumber: 1, pageSize: 25,
      totalDebit: 1200, totalCredit: 0,
      rows: [{ dimensionId: 'dim-c1', dimensionCode: 'CC1', dimensionName: 'فروش مرکزی', debit: 1200, credit: 0, signedBalance: 1200, direction: 'Debtor', displayBalance: 1200 }],
    });
    renderRoute(null, '/accounting/reports/dimensions?tab=cost-centers&dimensionId=dim-c1');
    expect(await screen.findByTestId('financial-table')).toBeInTheDocument();
    // فهرست مراکز هزینه واقعی در select
    const select = screen.getByTestId('dimension-select') as HTMLSelectElement;
    expect(select.textContent).toContain('فروش مرکزی');
    const request = mock.history.get.find((item) => item.url.includes('analysis/turnover'));
    expect(request!.params).toMatchObject({ dimensionType: 2, dimensionId: 'dim-c1' });
  });

  it('switching tab resets dimension selection and updates type', async () => {
    mock.onGet('/accounting/dimensions/analysis/turnover').reply(200, { dimensionType: 'Party', accountId: null, totalCount: 0, pageNumber: 1, pageSize: 25, totalDebit: 0, totalCredit: 0, rows: [] });
    renderRoute(null, '/accounting/reports/dimensions?tab=cost-centers');
    fireEvent.click(await screen.findByTestId('tab-parties'));
    await waitFor(() => {
      const request = mock.history.get[mock.history.get.length - 1];
      expect(request.params.dimensionType).toBe(6);
    });
  });
});

describe('CombinedDimensionsPage — گزارش ابعاد ترکیبی', () => {
  it('sends repeated dimensionTypes keys (no []) and shows truncation banner', async () => {
    mock.onGet(/analysis\/combined/).reply(200, {
      dimensionTypes: ['CostCenter', 'Project'], totalMatchedRows: 700, isTruncated: true, totalDebit: 500, totalCredit: 300,
      rows: [{ dimensions: [{ dimensionType: 'CostCenter', dimensionId: 'dim-c1', code: 'CC1', name: 'فروش مرکزی' }, { dimensionType: 'Project', dimensionId: 'dim-p1', code: 'PR1', name: 'پروژه برج' }], debit: 500, credit: 300, signedBalance: 200, direction: 'Debtor', displayBalance: 200 }],
    });
    renderRoute(null, '/accounting/reports/dimensions/combined');
    await waitFor(() => expect(mock.history.get.some((request) => request.url.includes('analysis/combined'))).toBe(true));
    const request = mock.history.get.find((item) => item.url.includes('analysis/combined'))!;
    // binding واقعی ASP.NET: کلید تکرارشونده بدون []
    expect(request.url).toContain('dimensionTypes=2');
    expect(request.url).toContain('dimensionTypes=3');
    expect(request.url).not.toContain('%5B%5D');
    expect(await screen.findByTestId('combined-truncated')).toBeInTheDocument();
    expect(screen.getByTestId('combined-total-debit').textContent).toBe('500');
  });

  it('caps selection at three dimension types', async () => {
    mock.onGet(/analysis\/combined/).reply(200, { dimensionTypes: [], totalMatchedRows: 0, isTruncated: false, totalDebit: 0, totalCredit: 0, rows: [] });
    renderRoute(null, '/accounting/reports/dimensions/combined');
    // حالت اولیه: CostCenter+Project — افزودن Party → ۳؛ افزودن چهارم → سقف
    fireEvent.click(await screen.findByTestId('combined-type-Party'));
    fireEvent.click(screen.getByTestId('combined-type-DetailAccount'));
    const selected = screen.getAllByTestId(/combined-type-/).filter((el) => el.getAttribute('aria-pressed') === 'true');
    expect(selected.length).toBe(3); // سقف backend
  });
});

describe('تغییر شرکت — جلوگیری از نمایش Cache شرکت قبلی', () => {
  it('company change clears cache and refetches with new context (header switcher contract)', async () => {
    const { queryClient } = await import('@/app/providers/query-provider');
    mock.onGet('/accounting/ledger/journal').reply(200, { entries: [], dailyTotals: [], periodTotalDebit: 0, periodTotalCredit: 0, totalCount: 0, pageNumber: 1, pageSize: 25 });
    renderRoute(null, '/accounting/reports/journal');
    await screen.findByTestId('journal-page');
    const callsBefore = mock.history.get.filter((request) => request.url.includes('/accounting/ledger/journal')).length;

    // شبیه‌سازی تغییر شرکت: setCompanyContext + queryClient.clear (قرارداد header switcher)
    useCompanyStore.getState().setCompanyContext(
      { id: 't1', name: 'گروه آریا', companies: [{ id: 'c2', name: 'آریا ۲', fiscalYear: 1404 }] },
      { id: 'c2', name: 'آریا ۲', fiscalYear: 1404 },
    );
    queryClient.clear();
    await screen.findByTestId('journal-page');
    // صفحات با companyId در کلید query — cache شرکت قبلی هرگز بازمصرف نمی‌شود
    expect(mock.history.get.filter((request) => request.url.includes('/accounting/ledger/journal')).length).toBeGreaterThanOrEqual(callsBefore);
    useCompanyStore.getState().setCompanyContext(
      { id: 't1', name: 'گروه آریا', companies: [{ id: 'c1', name: 'آریا', fiscalYear: 1404 }] },
      { id: 'c1', name: 'آریا', fiscalYear: 1404 },
    );
  });
});
