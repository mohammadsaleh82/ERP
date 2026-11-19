/** فیلتر بازه زمانی داشبورد — persist در URL و بازگشت state (بدون پرش صفحه) */
import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import AxiosMockAdapter from 'axios-mock-adapter';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { MemoryRouter, Routes, Route, useLocation } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { apiClient } from '@/core/api/client';
import { useAuthStore } from '@/core/auth/auth.store';
import { useCompanyStore } from '@/core/company/company.store';
import { DashboardPage } from '@/features/dashboard/pages/dashboard-page';

let mock: AxiosMockAdapter;
let queryClient: QueryClient;

const LocationProbe = () => {
  const location = useLocation();
  return <div data-testid="location-probe">{location.pathname}{location.search}</div>;
};

beforeEach(() => {
  mock = new AxiosMockAdapter(apiClient, { onNoMatch: 'throwException' });
  useAuthStore.getState().setAuth(
    { userId: 'u1', email: 'a@b.c', fullName: 'مدیر سیستم', roles: [], permissions: ['accounting:documents:read', 'accounting:ledger:read'] },
    'tok', 't1', 'c1',
  );
  useCompanyStore.getState().setCompanyContext(
    { id: 't1', name: 'گروه آریا', companies: [{ id: 'c1', name: 'آریا', fiscalYear: 1404 }] },
    { id: 'c1', name: 'آریا', fiscalYear: 1404 },
  );
  mock.onGet().reply(200, []);
});
afterEach(() => {
  mock.restore();
  useAuthStore.getState().clearAuth();
  useCompanyStore.getState().clearCompanyContext();
});

describe('Dashboard range filter URL persistence', () => {
  it('reflects initial from/to from URL and updates query string on change', async () => {
    queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={['/?from=2026-01-01&to=2026-11-19']}>
          <Routes>
            <Route path="/" element={<DashboardPage />} />
            <Route path="*" element={<LocationProbe />} />
          </Routes>
          <LocationProbe />
        </MemoryRouter>
      </QueryClientProvider>,
    );

    const fromInput = screen.getAllByLabelText('از تاریخ')[0] as HTMLInputElement;
    const toInput = screen.getAllByLabelText('تا تاریخ')[0] as HTMLInputElement;
    expect(fromInput.value).toBe('2026-01-01');
    expect(toInput.value).toBe('2026-11-19');

    fireEvent.change(fromInput, { target: { value: '2026-02-01' } });
    await waitFor(() => {
      expect((screen.getAllByTestId('location-probe')[0].textContent).includes('from=2026-02-01')).toBe(true);
    });
    // balances با بازه جدید مجدداً fetch می‌شود
    await waitFor(() => {
      expect(mock.history.get.some((call) => call.url.includes('/Ledger/balances') && call.params?.from === '2026-02-01')).toBe(true);
    });
  });
});
