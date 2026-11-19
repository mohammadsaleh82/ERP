/** لیست اسناد — فیلترها در URL، مرتب‌سازی، صفحه‌بندی، جزئیات سریع */
import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import AxiosMockAdapter from 'axios-mock-adapter';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { apiClient } from '@/core/api/client';
import { useAuthStore } from '@/core/auth/auth.store';
import { useCompanyStore } from '@/core/company/company.store';
import { VouchersPage } from '@/features/documents';

let mock: AxiosMockAdapter;

const docs = Array.from({ length: 3 }, (_, index) => ({
  id: `d${index + 1}`, tenantId: 't1', companyId: 'c1', fiscalYearId: 'fy1', fiscalPeriodId: 'p1',
  documentNumber: 101 + index, documentDate: '2026-03-01', description: `سند شماره ${index + 1}`,
  documentType: 'General', status: index === 0 ? 'Draft' : index === 1 ? 'PendingApproval' : 'Posted',
  referenceNumber: null, sourceModule: null, sourceDocumentId: null, reversedDocumentId: null,
  totalDebit: '1000', totalCredit: '1000', currencyCode: 'IRR', rejectionReason: null, createdBy: 'u1',
  createdAtUtc: 'x', submittedBy: null, submittedAtUtc: null, approvedBy: null, approvedAtUtc: null, postedBy: null, postedAtUtc: null, linesCount: 2,
}));

beforeEach(() => {
  mock = new AxiosMockAdapter(apiClient, { onNoMatch: 'throwException' });
  useAuthStore.getState().setAuth(
    { userId: 'u1', email: 'a@b.c', fullName: 'حسابدار', roles: [], permissions: ['accounting:documents:read', 'accounting:documents:create'] },
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

const renderPage = (initialEntry = '/accounting/documents') => {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[initialEntry]}>
        <VouchersPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

describe('VouchersPage', () => {
  it('lists documents with status badges and totals', async () => {
    mock.onGet('/Documents').reply(200, { items: docs, totalCount: 3, pageNumber: 1, pageSize: 20 });
    renderPage();
    expect(await screen.findByTestId('documents-table')).toBeInTheDocument();
    expect(screen.getByText('سند شماره 1')).toBeInTheDocument();
    expect(screen.getAllByText('ثبت قطعی').length).toBeGreaterThanOrEqual(1);
  });

  it('sends real filter params and keeps them in URL', async () => {
    mock.onGet('/Documents').reply(200, { items: [], totalCount: 0, pageNumber: 1, pageSize: 20 });
    renderPage('/accounting/documents?status=Posted&from=2026-01-01&to=2026-03-01&number=101&page=2');
    await waitFor(() => expect(mock.history.get.length).toBeGreaterThan(0));
    const request = mock.history.get[mock.history.get.length - 1];
    // axios-mock پارامترها را در request.params نگه می‌دارد
    expect(request.params).toMatchObject({
      status: 'Posted',
      fromDate: '2026-01-01',
      toDate: '2026-03-01',
      documentNumber: 101,
      pageNumber: 2,
    });
    // صفحه از فیلترهای URL بازسازی می‌شود؛ داده خالی → حالت خالی
    expect(screen.getByText('سندی یافت نشد')).toBeInTheDocument();
  });

  it('changes sortBy via column toggle', async () => {
    mock.onGet('/Documents').reply(200, { items: docs, totalCount: 3, pageNumber: 1, pageSize: 20 });
    renderPage();
    await screen.findByTestId('documents-table');
    fireEvent.click(screen.getByRole('button', { name: 'مرتب‌سازی بر اساس شماره سند' }));
    await waitFor(() => {
      expect(mock.history.get[mock.history.get.length - 1].params.sortBy).toBe('DocumentNumber:desc');
    });
    fireEvent.click(screen.getByRole('button', { name: 'مرتب‌سازی بر اساس شماره سند' }));
    await waitFor(() => {
      expect(mock.history.get[mock.history.get.length - 1].params.sortBy).toBe('DocumentNumber:asc');
    });
  });

  it('paginates with server params', async () => {
    mock.onGet('/Documents').reply(200, { items: docs, totalCount: 45, pageNumber: 1, pageSize: 20 });
    renderPage();
    await screen.findByTestId('documents-table');
    expect(screen.getByText('۴۵ سند · صفحه ۱ از ۳')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('next-page'));
    await waitFor(() => expect(mock.history.get[mock.history.get.length - 1].params.pageNumber).toBe(2));
  });

  it('shows error state on failure', async () => {
    mock.onGet('/Documents').networkError();
    renderPage();
    expect(await screen.findByRole('alert')).toBeInTheDocument();
  });
});
