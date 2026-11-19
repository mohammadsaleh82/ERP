/**
 * فرم سند — تعاملی: توازن زنده، کپی/جابه‌جایی/حذف ردیف، اعتبارسنجی‌ها با پیام backend،
 * payload واقعی (amount→number)، خطای دوره بسته، خطای شبکه، هم‌زمانی.
 */
import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import AxiosMockAdapter from 'axios-mock-adapter';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { apiClient } from '@/core/api/client';
import { useAuthStore } from '@/core/auth/auth.store';
import { useCompanyStore } from '@/core/company/company.store';
import { VoucherForm } from '@/features/documents/components/voucher-form';
import { toast } from '@/core/notifications/toast';

vi.mock('@/core/notifications/toast', () => ({
  toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() },
}));

let mock: AxiosMockAdapter;

const accounts = [
  { id: 'a-cash', tenantId: 't1', companyId: 'c1', parentAccountId: null, code: '110101', fullCode: '110101', name: 'صندوق', description: null, level: 'Detail', nature: 'Asset', normalBalance: 'Debit', isActive: true, isSystemAccount: false, allowPosting: true, hasChildren: false, requireCostCenter: false, requireProject: false, requireDetailAccount: false, createdAtUtc: 'x', lastModifiedAtUtc: null },
  { id: 'a-rev', tenantId: 't1', companyId: 'c1', parentAccountId: null, code: '400001', fullCode: '400001', name: 'درآمد فروش', description: null, level: 'Detail', nature: 'Revenue', normalBalance: 'Credit', isActive: true, isSystemAccount: false, allowPosting: true, hasChildren: false, requireCostCenter: false, requireProject: false, requireDetailAccount: false, createdAtUtc: 'x', lastModifiedAtUtc: null },
  { id: 'a-dim', tenantId: 't1', companyId: 'c1', parentAccountId: null, code: '500001', fullCode: '500001', name: 'هزینه پرسنلی', description: null, level: 'Detail', nature: 'Expense', normalBalance: 'Debit', isActive: true, isSystemAccount: false, allowPosting: true, hasChildren: false, requireCostCenter: true, requireProject: false, requireDetailAccount: true, createdAtUtc: 'x', lastModifiedAtUtc: null },
];

beforeEach(() => {
  mock = new AxiosMockAdapter(apiClient, { onNoMatch: 'throwException' });
  useAuthStore.getState().setAuth(
    { userId: 'u1', email: 'a@b.c', fullName: 'حسابدار', roles: [], permissions: ['accounting:documents:create', 'accounting:documents:update', 'accounting:documents:submit'] },
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
  mock.onGet('/FiscalYears/fy1/periods').reply(200, [
    { id: 'p1', fiscalYearId: 'fy1', periodNumber: 12, name: 'اسفند', startDate: '2026-02-20', endDate: '2026-03-20', status: 'Open', isAdjustmentPeriod: false },
    { id: 'p2', fiscalYearId: 'fy1', periodNumber: 11, name: 'بهمن', startDate: '2026-01-21', endDate: '2026-02-19', status: 'Closed', isAdjustmentPeriod: false },
  ]);
});
afterEach(() => {
  mock.restore();
  useAuthStore.getState().clearAuth();
});

const renderForm = (props = {}) => {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <VoucherForm {...props} />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

/** انتخاب حساب ردیف n از picker */
const pickAccount = async (row: number, accountId: string) => {
  fireEvent.click(screen.getByTestId(`line-account-picker-${row}`));
  const picker = screen.getByTestId(`line-account-picker-${row}`);
  const input = within(picker).getByRole('textbox');
  fireEvent.focus(input); // باز شدن لیست با فوکوس
  await waitFor(() => expect(within(picker).getAllByRole('option').length).toBeGreaterThan(0));
  const option = within(picker).getAllByRole('option').find((el) => el.textContent?.includes(accounts.find((a) => a.id === accountId)!.name));
  fireEvent.click(within(option!).getByRole('button')); // کلیک روی دکمه داخل option
};

const fillRow = async (row: number, accountId: string, side: 'debit' | 'credit', amount: string) => {
  if (!screen.queryByTestId(`line-row-${row}`)) {
    fireEvent.click(screen.getByTestId('add-row'));
  }
  await pickAccount(row, accountId);
  fireEvent.change(screen.getByTestId(`line-${side}-${row}`), { target: { value: amount } });
  fireEvent.change(screen.getByLabelText(`شرح ردیف ${row}`), { target: { value: `شرح ردیف ${row}` } });
};

describe('VoucherForm — interactive', () => {
  it('shows live balanced state for balanced rows with decimal-safe sums', async () => {
    renderForm();
    await fillRow(1, 'a-cash', 'debit', '1250.25');
    await fillRow(2, 'a-rev', 'credit', '1250.25');
    await waitFor(() => expect(screen.getByTestId('balanced-badge')).toBeInTheDocument());
    expect(screen.getByTestId('total-debit').textContent).toBe('1,250.25');
    expect(screen.getByTestId('total-difference').textContent).toBe('0');
  });

  it('flags unbalanced with exact decimal difference (0.01 — no float drift)', async () => {
    renderForm();
    await fillRow(1, 'a-cash', 'debit', '100');
    await fillRow(2, 'a-rev', 'credit', '99.99');
    expect(screen.getByTestId('unbalanced-badge')).toBeInTheDocument();
    expect(screen.getByTestId('total-difference').textContent).toBe('0.01');
    fireEvent.click(screen.getByTestId('save-draft'));
    expect(screen.getByTestId('form-errors').textContent).toContain('برابر شود');
    expect(mock.history.post).toHaveLength(0);
  });

  it('rejects incomplete row (missing account/description/amount) with backend messages', async () => {
    renderForm();
    fireEvent.click(screen.getByTestId('save-draft'));
    const errors = screen.getByTestId('form-errors').textContent;
    expect(errors).toContain('شرح سند حسابداری الزامی است.');
    expect(errors).toContain('تاریخ سند الزامی است.');
    expect(errors).toContain('سند بدون ردیف قابل ثبت نیست');
    expect(mock.history.post).toHaveLength(0);
  });

  it('rejects non-positive amount with backend message', async () => {
    renderForm();
    await fillRow(1, 'a-cash', 'debit', '-5');
    fireEvent.click(screen.getByTestId('save-draft'));
    expect(screen.getByTestId('form-errors').textContent).toContain('مبلغ ردیف سند باید عددی مثبت و بزرگتر از صفر باشد.');
  });

  it('shows dimension requirement badges for detail-required accounts', async () => {
    renderForm();
    await fillRow(1, 'a-dim', 'debit', '100');
    expect(screen.getByTestId('line-dimensions-1').textContent).toContain('مرکز هزینه الزامی');
    expect(screen.getByTestId('line-dimensions-1').textContent).toContain('تفصیلی الزامی');
    fireEvent.click(screen.getByTestId('save-draft'));
    // شرح سند هم خالی است ولی خطای DimensionRequired باید بیاید
    expect(screen.getByTestId('form-errors').textContent).toContain('DimensionRequired');
  });

  it('warns closed period for date inside a closed period of selected year', async () => {
    renderForm();
    await waitFor(() => expect(screen.getByTestId('voucher-fiscal-year')).toHaveValue('fy1'));
    fireEvent.change(screen.getByTestId('voucher-date'), { target: { value: '2026-02-01' } }); // بهمن — بسته
    await waitFor(() => expect(screen.getByTestId('closed-period-warning').textContent).toContain('DocumentDateOutsideOpenPeriod')); // پس از رسیدن دوره‌ها
    fireEvent.click(screen.getByTestId('save-draft'));
    expect(screen.getByTestId('form-errors').textContent).toContain('دوره بسته');
    expect(mock.history.post).toHaveLength(0);
  });

  it('submits real CreateDocumentRequest with numeric amounts and idempotency key', async () => {
    renderForm();
    await waitFor(() => expect(screen.getByTestId('voucher-fiscal-year')).toHaveValue('fy1'));
    fireEvent.change(screen.getByTestId('voucher-date'), { target: { value: '2026-03-01' } });
    fireEvent.change(screen.getByTestId('voucher-description'), { target: { value: 'فروش نقدی' } });
    await fillRow(1, 'a-cash', 'debit', '۱۲٬۵۰۰'); // ارقام فارسی
    await fillRow(2, 'a-rev', 'credit', '12500');

    mock.onPost('/Documents').reply(201, {
      id: 'doc1', documentNumber: 42, status: 'Draft', totalDebit: '12500', totalCredit: '12500', linesCount: 2,
    });
    fireEvent.click(screen.getByTestId('save-draft'));

    await waitFor(() => expect(mock.history.post).toHaveLength(1));
    const body = JSON.parse(mock.history.post[0].data);
    expect(body.documentDate).toBe('2026-03-01');
    expect(body.documentType).toBe('General');
    expect(body.lines[0]).toMatchObject({ accountId: 'a-cash', side: 'Debit', amount: 12500 }); // number برای binding decimal سرور
    expect(body.idempotencyKey).toBeTruthy();
  });

  it('copies and deletes rows, reorder allowed', async () => {
    renderForm();
    await fillRow(1, 'a-cash', 'debit', '100');
    fireEvent.click(screen.getByTestId('copy-row-1'));
    expect(screen.getByTestId('line-row-2')).toBeInTheDocument();
    expect(screen.getByTestId('line-debit-2')).toHaveValue('100');
    // جابه‌جایی
    fireEvent.click(screen.getByRole('button', { name: 'جابه‌جایی ردیف 2 به بالا' }));
    expect(screen.getByTestId('line-row-1')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('delete-row-2'));
    expect(screen.queryByTestId('line-row-2')).toBeNull();
  });

  it('network failure surfaces Persian error toast', async () => {
    renderForm();
    fireEvent.change(screen.getByTestId('voucher-date'), { target: { value: '2026-03-01' } });
    fireEvent.change(screen.getByTestId('voucher-description'), { target: { value: 'سند تست' } });
    await fillRow(1, 'a-cash', 'debit', '100');
    await fillRow(2, 'a-rev', 'credit', '100');

    mock.onPost('/Documents').networkError();
    fireEvent.click(screen.getByTestId('save-draft'));
    await waitFor(() => expect(toast.error).toHaveBeenCalled());
    // interceptor خطای شبکه را به پیام فارسی استاندارد تبدیل می‌کند
    expect((toast.error as ReturnType<typeof vi.fn>).mock.calls[0][1]).toContain('ارتباط با سرور');
  });

  it('backend validation error (Accounting.DocumentDateOutsideOpenPeriod) shown as toast on save', async () => {
    renderForm();
    fireEvent.change(screen.getByTestId('voucher-date'), { target: { value: '2026-03-01' } });
    fireEvent.change(screen.getByTestId('voucher-description'), { target: { value: 'سند تست' } });
    await fillRow(1, 'a-cash', 'debit', '100');
    await fillRow(2, 'a-rev', 'credit', '100');

    mock.onPost('/Documents').reply(422, { code: 'Accounting.DocumentDateOutsideOpenPeriod', detail: 'تاریخ سند خارج از دوره باز است' });
    fireEvent.click(screen.getByTestId('save-draft'));
    await waitFor(() => expect(toast.error).toHaveBeenCalled());
    expect((toast.error as ReturnType<typeof vi.fn>).mock.calls[0][1]).toContain('تاریخ سند خارج از دوره باز است');
  });

  it('edit mode PUTs UpdateDocumentRequest on existing draft', async () => {
    const draft = {
      id: 'doc9', tenantId: 't1', companyId: 'c1', fiscalYearId: 'fy1', fiscalPeriodId: 'p1', documentNumber: 7,
      documentDate: '2026-03-01', description: 'پیش‌نویس قدیمی', documentType: 'General', status: 'Draft',
      referenceNumber: null, sourceModule: null, sourceDocumentId: null, reversedDocumentId: null,
      totalDebit: '100', totalCredit: '100', currencyCode: 'IRR', rejectionReason: null, createdBy: 'u1',
      createdAtUtc: 'x', submittedBy: null, submittedAtUtc: null, approvedBy: null, approvedAtUtc: null, postedBy: null, postedAtUtc: null,
      linesCount: 2, isBalanced: true,
      lines: [
        { id: 'l1', accountId: 'a-cash', accountFullCode: '110101', accountName: 'صندوق', lineNumber: 1, side: 'Debit', debitAmount: '100', creditAmount: '0', description: 'قفل صندوق', costCenterId: null, projectId: null, detailAccountId: null, foreignCurrencyCode: null, foreignCurrencyAmount: null, exchangeRate: 1 },
        { id: 'l2', accountId: 'a-rev', accountFullCode: '400001', accountName: 'درآمد فروش', lineNumber: 2, side: 'Credit', debitAmount: '0', creditAmount: '100', description: 'درآمد', costCenterId: null, projectId: null, detailAccountId: null, foreignCurrencyCode: null, foreignCurrencyAmount: null, exchangeRate: 1 },
      ],
    };
    renderForm({ editingDocument: draft });
    expect(screen.getByText('ویرایش پیش‌نویس سند 7')).toBeInTheDocument();
    mock.onPut('/Documents/doc9').reply(200, { ...draft, description: 'اصلاح شد' });
    fireEvent.click(screen.getByTestId('save-draft'));
    await waitFor(() => expect(mock.history.put).toHaveLength(1));
    const body = JSON.parse(mock.history.put[0].data);
    expect(body.lines[0].amount).toBe(100);
    expect(body.lines[0].description).toBe('قفل صندوق');
  });
});
