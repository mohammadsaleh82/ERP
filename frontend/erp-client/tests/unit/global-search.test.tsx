/** جست‌وجوی سراسری: debounce، حالت‌ها، بخش‌های بدون API */
import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import AxiosMockAdapter from 'axios-mock-adapter';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { apiClient } from '@/core/api/client';
import { useAuthStore } from '@/core/auth/auth.store';
import { GlobalSearch } from '@/features/search/global-search';

let mock: AxiosMockAdapter;

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
  mock = new AxiosMockAdapter(apiClient, { onNoMatch: 'throwException' });
  useAuthStore.setState({
    user: { userId: 'u1', email: 'a@b.c', fullName: 'کاربر', roles: [], permissions: ['accounting:documents:read', 'accounting:accounts:read'] },
  });
});
afterEach(() => {
  vi.useRealTimers();
  mock.restore();
});

describe('GlobalSearch', () => {
  it('debounces input: exactly one request after rapid typing stops', async () => {
    mock.onGet('/Accounts').reply(200, { items: [{ id: 'a1', code: '1101', fullCode: '1101', name: 'صندوق', level: 4, isActive: true }], totalCount: 1, pageNumber: 1, pageSize: 10 });

    const { openSearch } = setup();
    openSearch();
    const input = screen.getByTestId('global-search-input');

    fireEvent.change(input, { target: { value: 'صن' } });
    fireEvent.change(input, { target: { value: 'صند' } });
    fireEvent.change(input, { target: { value: 'صندوق' } });

    await vi.advanceTimersByTimeAsync(400);
    await waitFor(() => expect(screen.getByText('صندوق')).toBeInTheDocument());

    const accountCalls = mock.history.get.filter((call) => call.url.includes('/Accounts'));
    expect(accountCalls).toHaveLength(1);
    expect(accountCalls[0].params).toMatchObject({ searchTerm: 'صندوق' });
  });

  it('documents search only fires for numeric terms', async () => {
    mock.onGet('/Documents').reply(200, { items: [{ id: 'd1', documentNumber: 101, documentDate: '2026-11-01', totalDebit: 500, status: 'Posted' }], totalCount: 1, pageNumber: 1, pageSize: 10 });

    const { openSearch } = setup();
    openSearch();
    fireEvent.change(screen.getByTestId('global-search-input'), { target: { value: '101' } });
    await vi.advanceTimersByTimeAsync(400);
    await waitFor(() => expect(screen.getByText('سند ۱۰۱')).toBeInTheDocument());
    expect(mock.history.get.filter((call) => call.url.includes('/Documents'))).toHaveLength(1);

    fireEvent.change(screen.getByTestId('global-search-input'), { target: { value: 'bank' } });
    await vi.advanceTimersByTimeAsync(400);
    // متن غیر عددی → درخواست اسناد جدید نمی‌رود
    expect(mock.history.get.filter((call) => call.url.includes('/Documents'))).toHaveLength(1);
  });

  it('shows empty state and backend-required badges for parties/reports sections', async () => {
    mock.onGet('/Accounts').reply(200, { items: [], totalCount: 0, pageNumber: 1, pageSize: 10 });

    const { openSearch } = setup();
    openSearch();
    fireEvent.change(screen.getByTestId('global-search-input'), { target: { value: 'xyz' } });
    await vi.advanceTimersByTimeAsync(400);

    await waitFor(() => expect(screen.getByText('حسابی با این کد/نام یافت نشد')).toBeInTheDocument());
    expect(screen.getByText('نیاز Backend: API جست‌وجو موجود نیست')).toBeInTheDocument(); // اشخاص/گزارش‌ها
    expect(screen.getByText('نتیجه‌ای یافت نشد')).toBeInTheDocument();
  });

  it('renders isolated error message when accounts API fails', async () => {
    mock.onGet('/Accounts').reply(500, { code: 'Http.500', detail: 'خطای سرور' });

    const { openSearch } = setup();
    openSearch();
    fireEvent.change(screen.getByTestId('global-search-input'), { target: { value: 'صندوق' } });
    await vi.advanceTimersByTimeAsync(400);

    expect(await screen.findByText('جست‌وجوی حساب‌ها ناموفق بود')).toBeInTheDocument();
  });
});

function setup() {
  const utils = render(
    <MemoryRouter>
      <GlobalSearch />
    </MemoryRouter>,
  );
  const trigger = screen.getByTestId('global-search-trigger');
  fireEvent.click(trigger);
  return { ...utils, openSearch: () => undefined };
}
