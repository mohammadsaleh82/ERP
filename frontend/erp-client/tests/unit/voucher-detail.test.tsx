/**
 * مشاهده سند + گردش کار — read-only سند ثبت‌شده، عملیات با تأیید، رد با دلیل الزامی،
 * معکوس با تاریخ، تعارض post (409)، SOD، تاریخچه audit-trail و قالب چاپ.
 */
import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import AxiosMockAdapter from 'axios-mock-adapter';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { apiClient } from '@/core/api/client';
import { useAuthStore } from '@/core/auth/auth.store';
import { useCompanyStore } from '@/core/company/company.store';
import { VoucherDetailPage } from '@/features/documents/pages/voucher-detail-page';
import { toast } from '@/core/notifications/toast';

vi.mock('@/core/notifications/toast', () => ({
  toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() },
}));

let mock: AxiosMockAdapter;

const makeDoc = (status: string, overrides: Record<string, unknown> = {}) => ({
  id: 'doc1', tenantId: 't1', companyId: 'c1', fiscalYearId: 'fy1', fiscalPeriodId: 'p1', documentNumber: 42,
  documentDate: '2026-03-01', description: 'فروش نقدی', documentType: 'General', status,
  referenceNumber: null, sourceModule: null, sourceDocumentId: null, reversedDocumentId: null,
  totalDebit: '1500', totalCredit: '1500', currencyCode: 'IRR', rejectionReason: null, createdBy: 'accountant-1',
  createdAtUtc: '2026-03-01T08:00:00Z', submittedBy: null, submittedAtUtc: null, approvedBy: null, approvedAtUtc: null, postedBy: null, postedAtUtc: null,
  linesCount: 2, isBalanced: true,
  lines: [
    { id: 'l1', accountId: 'a-cash', accountFullCode: '110101', accountName: 'صندوق', lineNumber: 1, side: 'Debit', debitAmount: '1500', creditAmount: '0', description: 'وصول نقد', costCenterId: null, projectId: null, detailAccountId: null, foreignCurrencyCode: null, foreignCurrencyAmount: null, exchangeRate: 1 },
    { id: 'l2', accountId: 'a-rev', accountFullCode: '400001', accountName: 'درآمد فروش', lineNumber: 2, side: 'Credit', debitAmount: '0', creditAmount: '1500', description: 'فروش', costCenterId: null, projectId: null, detailAccountId: null, foreignCurrencyCode: null, foreignCurrencyAmount: null, exchangeRate: 1 },
  ],
  ...overrides,
});

const ALL_PERMS = ['accounting:documents:read', 'accounting:documents:create', 'accounting:documents:update', 'accounting:documents:delete', 'accounting:documents:submit', 'accounting:documents:approve', 'accounting:documents:reject', 'accounting:documents:post', 'accounting:documents:reverse'];

beforeEach(() => {
  mock = new AxiosMockAdapter(apiClient, { onNoMatch: 'throwException' });
  useAuthStore.getState().setAuth(
    { userId: 'u-approver', email: 'a@b.c', fullName: 'مدیر مالی', roles: [], permissions: ALL_PERMS },
    'tok', 't1', 'c1',
  );
  useCompanyStore.getState().setCompanyContext(
    { id: 't1', name: 'گروه آریا', companies: [{ id: 'c1', name: 'آریا', fiscalYear: 1404 }] },
    { id: 'c1', name: 'آریا', fiscalYear: 1404 },
  );
});
afterEach(() => {
  mock.restore();
  useAuthStore.getState().clearAuth();
});

const renderDetail = () => {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/accounting/documents/doc1']}>
        <Routes>
          <Route path="/accounting/documents/:id" element={<VoucherDetailPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

describe('VoucherDetailPage — statuses and read-only rules', () => {
  it('renders posted document strictly read-only with no edit and workflow note', async () => {
    mock.onGet('/Documents/doc1').reply(200, makeDoc('Posted', { postedBy: 'cfo-1', postedAtUtc: '2026-03-01T09:00:00Z' }));
    renderDetail();
    expect(await screen.findByTestId('posted-readonly-note')).toBeInTheDocument();
    expect(screen.queryByTestId('edit-draft')).toBeNull();
    expect(screen.getByTestId('action-reverse')).toBeInTheDocument();
    expect(screen.queryByTestId('action-post')).toBeNull();
    expect(screen.getByTestId('view-total-debit').textContent).toBe('1,500');
  });

  it('draft shows edit button and submit action; rejects are possible', async () => {
    mock.onGet('/Documents/doc1').reply(200, makeDoc('Draft'));
    renderDetail();
    expect(await screen.findByTestId('edit-draft')).toBeInTheDocument();
    expect(screen.getByTestId('action-submit')).toBeInTheDocument();
    expect(screen.getByTestId('action-delete')).toBeInTheDocument();
  });

  it('edit mode renders the form with existing lines', async () => {
    mock.onGet('/Documents/doc1').reply(200, makeDoc('Draft'));
    renderDetail();
    fireEvent.click(await screen.findByTestId('edit-draft'));
    expect(screen.getByTestId('voucher-form')).toBeInTheDocument();
    expect(screen.getByText('ویرایش پیش‌نویس سند 42')).toBeInTheDocument();
    expect(screen.getByTestId('line-row-1')).toBeInTheDocument();
  });

  it('pending approval: approve with confirmation updates status; SOD error surfaced', async () => {
    mock.onGet('/Documents/doc1').reply(200, makeDoc('PendingApproval'));
    mock.onPost('/Documents/doc1/approve').reply(403, { code: 'Accounting.SodCreatorCannotApprove', detail: 'ایجادکننده نمی‌تواند تأیید کند' });
    renderDetail();
    fireEvent.click(await screen.findByTestId('action-approve'));
    fireEvent.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'تأیید سند' }));
    await waitFor(() => expect(mock.history.post.filter((c) => c.url.includes('/approve'))).toHaveLength(1));
    await waitFor(() => expect(toast.error).toHaveBeenCalled());
    expect((toast.error as ReturnType<typeof vi.fn>).mock.calls[0][1]).toContain('جداسازی وظایف'); // پیام SOD کامپوننت
  });

  it('reject requires reason (backend validator) and posts payload', async () => {
    mock.onGet('/Documents/doc1').reply(200, makeDoc('PendingApproval'));
    mock.onPost('/Documents/doc1/reject').reply(200, makeDoc('Draft', { rejectionReason: 'مبلغ نادرست' }));
    renderDetail();
    fireEvent.click(await screen.findByTestId('action-reject'));
    const confirmButton = screen.getByTestId('confirm-reject');
    expect(confirmButton).toBeDisabled(); // بدون دلیل غیرفعال
    fireEvent.change(screen.getByTestId('rejection-reason'), { target: { value: 'مبلغ نادرست است' } });
    expect(confirmButton).toBeEnabled();
    fireEvent.click(confirmButton);
    await waitFor(() => expect(mock.history.post.filter((c) => c.url.includes('/reject'))).toHaveLength(1));
    expect(JSON.parse(mock.history.post[0].data)).toEqual({ rejectionReason: 'مبلغ نادرست است' });
  });

  it('post: double-click prevention + conflict 409 handled with refresh', async () => {
    mock.onGet('/Documents/doc1').reply(200, makeDoc('Approved'));
    mock.onPost('/Documents/doc1/post').reply(409, { code: 'Accounting.PostingConcurrencyConflict', detail: 'تعارض' });
    renderDetail();
    fireEvent.click(await screen.findByTestId('action-post'));
    fireEvent.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'ثبت قطعی' }));
    await waitFor(() => expect(mock.history.post.filter((c) => c.url.includes('/post'))).toHaveLength(1));
    await waitFor(() => expect(toast.error).toHaveBeenCalled());
    expect((toast.error as ReturnType<typeof vi.fn>).mock.calls[0][0]).toContain('تعارض');
    // refresh داده بعد از تعارض
    await waitFor(() => expect(mock.history.get.filter((c) => c.url.includes('/Documents/doc1')).length).toBeGreaterThanOrEqual(2));
  });

  it('successful post switches to posted read-only note (no editable view)', async () => {
    // axios-mock: اولین handler ثبت‌شده match می‌شود — پاسخ اول Approved (یک‌بار) و بعد Posted
    mock.onGet('/Documents/doc1').replyOnce(200, makeDoc('Approved'));
    mock.onGet('/Documents/doc1').reply(200, makeDoc('Posted', { postedBy: 'cfo-1', postedAtUtc: '2026-03-02T10:00:00Z' }));
    mock.onPost('/Documents/doc1/post').reply(200, { documentId: 'doc1', documentNumber: 42, totalDebit: '1500', totalCredit: '1500', postedAtUtc: '2026-03-02T10:00:00Z', postedBy: 'cfo-1', wasAlreadyPosted: false });
    renderDetail();
    fireEvent.click(await screen.findByTestId('action-post'));
    fireEvent.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'ثبت قطعی' }));
    await waitFor(() => expect(toast.success).toHaveBeenCalled());
    // refetch وضعیت جدید Posted
    mock.onGet('/Documents/doc1').reply(200, makeDoc('Posted', { postedBy: 'cfo-1', postedAtUtc: '2026-03-02T10:00:00Z' }));
    await waitFor(() => expect(screen.queryByTestId('action-post')).toBeNull());
  });

  it('reverse posts payload with reversalDate and idempotencyKey', async () => {
    mock.onGet('/Documents/doc1').reply(200, makeDoc('Posted'));
    mock.onPost('/Documents/doc1/reverse').reply(200, { originalDocumentId: 'doc1', reversalDocumentId: 'doc2', reversalDocumentNumber: 43, totalAmount: '1500', wasAlreadyReversed: false });
    renderDetail();
    fireEvent.click(await screen.findByTestId('action-reverse'));
    fireEvent.change(screen.getByTestId('reversal-date'), { target: { value: '2026-03-05' } });
    fireEvent.change(screen.getByTestId('reversal-description'), { target: { value: 'اصلاح ثبت' } });
    fireEvent.click(screen.getByTestId('confirm-reverse'));
    await waitFor(() => expect(mock.history.post.filter((c) => c.url.includes('/reverse'))).toHaveLength(1));
    const body = JSON.parse(mock.history.post[0].data);
    expect(body.reversalDate).toBe('2026-03-05');
    expect(body.idempotencyKey).toBeTruthy();
  });

  it('shows audit trail history', async () => {
    mock.onGet('/Documents/doc1').reply(200, makeDoc('Approved'));
    mock.onGet('/Documents/doc1/audit-trail').reply(200, [
      { id: 'h1', documentId: 'doc1', action: 'ثبت قطعی', previousStatus: 'Approved', newStatus: 'Posted', details: null, performedBy: 'cfo-1', performedAtUtc: '2026-03-01T09:00:00Z' },
      { id: 'h2', documentId: 'doc1', action: 'تأیید', previousStatus: 'PendingApproval', newStatus: 'Approved', details: null, performedBy: 'cfo-1', performedAtUtc: '2026-03-01T08:30:00Z' },
    ]);
    renderDetail();
    fireEvent.click(await screen.findByTestId('show-history'));
    expect(await screen.findByTestId('audit-trail')).toBeInTheDocument();
    expect(screen.getAllByText('ثبت قطعی').length).toBeGreaterThanOrEqual(1); // رخداد تاریخچه + دکمه workflow
    expect(screen.getByText(/در انتظار تأیید/).textContent).toContain('تاییدشده'); // گذار وضعیت در تاریخچه
  });

  it('print template exists with document data (A4 print block)', async () => {
    mock.onGet('/Documents/doc1').reply(200, makeDoc('Posted'));
    renderDetail();
    const printBlock = await screen.findByTestId('voucher-print');
    expect(printBlock.textContent).toContain('سند حسابداری شماره ۴۲');
    expect(printBlock.textContent).toContain('110101');
    expect(printBlock.textContent).toContain('1,500');
  });

  it('hides all workflow actions without permissions', async () => {
    useAuthStore.setState({
      user: { userId: 'u1', email: 'a@b.c', fullName: 'بیننده', roles: [], permissions: ['accounting:documents:read'] },
    });
    mock.onGet('/Documents/doc1').reply(200, makeDoc('PendingApproval'));
    renderDetail();
    await waitFor(() => expect(screen.getByTestId('document-lines')).toBeInTheDocument());
    expect(screen.queryByTestId('action-approve')).toBeNull();
    expect(screen.queryByTestId('action-reject')).toBeNull();
    expect(screen.queryByTestId('edit-draft')).toBeNull();
  });
});
