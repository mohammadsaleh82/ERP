/** سال مالی و دوره‌ها: لیست، ایجاد، عملیات باز/بستن دوره با مجوز، تقویم شمسی نمایشی */
import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import AxiosMockAdapter from 'axios-mock-adapter';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { apiClient } from '@/core/api/client';
import { useAuthStore } from '@/core/auth/auth.store';
import { FiscalYearsPage } from '@/features/fiscal';

let mock: AxiosMockAdapter;

const years = [
  { id: 'fy1', tenantId: 't1', companyId: 'c1', yearNumber: 1404, name: 'سال مالی ۱۴۰۴', startDate: '2025-03-21', endDate: '2026-03-20', calendarType: 'Persian', status: 'Open', isClosed: false, openingBalancePosted: true },
  { id: 'fy2', tenantId: 't1', companyId: 'c1', yearNumber: 1403, name: 'سال مالی ۱۴۰۳', startDate: '2024-03-20', endDate: '2025-03-20', calendarType: 'Persian', status: 'Closed', isClosed: true, openingBalancePosted: true },
];

const periods = [
  { id: 'p1', fiscalYearId: 'fy1', periodNumber: 12, name: 'اسفند ۱۴۰۴', startDate: '2026-02-20', endDate: '2026-03-20', status: 'Open', isAdjustmentPeriod: false },
  { id: 'p2', fiscalYearId: 'fy1', periodNumber: 13, name: 'تعدیلات', startDate: '2026-03-21', endDate: '2026-03-25', status: 'Draft', isAdjustmentPeriod: true },
];

beforeEach(() => {
  mock = new AxiosMockAdapter(apiClient, { onNoMatch: 'throwException' });
  useAuthStore.getState().setAuth(
    { userId: 'u1', email: 'a@b.c', fullName: 'مدیر', roles: [], permissions: ['accounting:fiscal-years:read', 'accounting:fiscal-years:create', 'accounting:fiscal-years:update', 'accounting:periods:close'] },
    'tok', 't1', 'c1',
  );
});
afterEach(() => {
  mock.restore();
  useAuthStore.getState().clearAuth();
});

const renderPage = () => {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <FiscalYearsPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

describe('FiscalYearsPage', () => {
  it('lists real fiscal years with Persian display dates and status', async () => {
    mock.onGet('/FiscalYears').reply(200, years);
    renderPage();

    const yearCard = await screen.findByTestId('fiscal-year-1404');
    expect(yearCard.textContent).toContain('سال مالی ۱۴۰۴');
    expect(screen.getAllByText('باز').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('بسته قطعی').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText(/تقویم هجری شمسی/).length).toBeGreaterThanOrEqual(1); // متن split-JSX
    // نمایش شمسی — مرز تاریخ مالی ISO دست‌نخورده در payloadها
    expect(yearCard.textContent).toContain('۱۴۰۴/۰۱/۰۱ تا ۱۴۰۴/۱۲/۲۹');
  });

  it('validates create form: year number required, name required', async () => {
    mock.onGet('/FiscalYears').reply(200, years);
    renderPage();
    fireEvent.click(await screen.findByTestId('create-fiscal-year'));
    fireEvent.click(screen.getByTestId('fy-submit'));
    expect(await screen.findByText('شماره سال الزامی است')).toBeInTheDocument();
    expect(screen.getByText('نام سال مالی الزامی است')).toBeInTheDocument();
    expect(mock.history.post.filter((call) => call.url.includes('/FiscalYears'))).toHaveLength(0);
  });

  it('submits real CreateFiscalYearRequest with Persian calendar and monthly periods', async () => {
    mock.onGet('/FiscalYears').reply(200, years);
    mock.onPost('/FiscalYears').reply(201, years[0]);
    renderPage();

    fireEvent.click(await screen.findByTestId('create-fiscal-year'));
    fireEvent.change(screen.getByTestId('fy-year-number'), { target: { value: '1405' } });
    fireEvent.change(screen.getByTestId('fy-name'), { target: { value: 'سال مالی ۱۴۰۵' } });
    fireEvent.click(screen.getByTestId('fy-submit'));

    await waitFor(() => expect(mock.history.post).toHaveLength(1));
    const body = JSON.parse(mock.history.post[0].data);
    expect(body).toMatchObject({ yearNumber: 1405, calendarType: 'Persian', autoGeneratePeriods: 'Monthly', startDate: null, endDate: null });
  });

  it('shows periods with adjustment flag and close gated by periods:close permission', async () => {
    mock.onGet('/FiscalYears').reply(200, years);
    mock.onGet('/FiscalYears/fy1/periods').reply(200, periods);
    renderPage();

    fireEvent.click(await screen.findByTestId('detail-year-1404'));
    expect(await screen.findByTestId('periods-table')).toBeInTheDocument();
    expect(screen.getByText('اسفند ۱۴۰۴')).toBeInTheDocument();
    expect(screen.getAllByText('تعدیلات').length).toBeGreaterThanOrEqual(1); // نام + badge
    expect(screen.getByTestId('close-period-12')).toBeInTheDocument(); // مجوز periods:close دارد
  });

  it('hides period close action without periods:close permission', async () => {
    useAuthStore.setState({
      user: { userId: 'u1', email: 'a@b.c', fullName: 'کاربر', roles: [], permissions: ['accounting:fiscal-years:read'] },
    });
    mock.onGet('/FiscalYears').reply(200, years);
    mock.onGet('/FiscalYears/fy1/periods').reply(200, periods);
    renderPage();

    fireEvent.click(await screen.findByTestId('detail-year-1404'));
    await waitFor(() => expect(screen.getByTestId('periods-table')).toBeInTheDocument());
    expect(screen.queryByTestId('close-period-12')).toBeNull();
    expect(screen.queryByTestId('add-custom-period')).toBeNull(); // fiscal-years:update ندارد
  });

  it('sends real close period request after confirm', async () => {
    mock.onGet('/FiscalYears').reply(200, years);
    mock.onGet('/FiscalYears/fy1/periods').reply(200, periods);
    mock.onPost('/FiscalYears/fy1/periods/p1/close').reply(200, { ...periods[0], status: 'Closed' });
    renderPage();

    fireEvent.click(await screen.findByTestId('detail-year-1404'));
    fireEvent.click(await screen.findByTestId('close-period-12'));
    fireEvent.click(screen.getByText('بستن دوره'));
    await waitFor(() => expect(mock.history.post.filter((c) => c.url.includes('/periods/p1/close'))).toHaveLength(1));
  });
});
