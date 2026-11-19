/** محدودیت‌های مالی حذف/غیرفعال‌سازی + مجوزها — قیدهای واقعی backend */
import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import AxiosMockAdapter from 'axios-mock-adapter';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { apiClient } from '@/core/api/client';
import { useAuthStore } from '@/core/auth/auth.store';
import { AccountDetailsPanel } from '@/features/accounts/components/account-details-panel';
import { toast } from '@/core/notifications/toast';

vi.mock('@/core/notifications/toast', () => ({
  toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() },
}));

let mock: AxiosMockAdapter;

const makeAccount = (overrides: Record<string, unknown> = {}) => ({
  id: 'a1', tenantId: 't1', companyId: 'c1', parentAccountId: null, code: '110101', fullCode: '110101', name: 'صندوق',
  description: null, level: 'Detail', nature: 'Asset', normalBalance: 'Debit', isActive: true, isSystemAccount: false,
  allowPosting: true, hasChildren: false, requireCostCenter: false, requireProject: false, requireDetailAccount: false,
  createdAtUtc: '2026-01-01T00:00:00Z', lastModifiedAtUtc: null, ...overrides,
});

beforeEach(() => {
  mock = new AxiosMockAdapter(apiClient, { onNoMatch: 'throwException' });
  useAuthStore.getState().setAuth(
    { userId: 'u1', email: 'a@b.c', fullName: 'مدیر', roles: [], permissions: ['accounting:accounts:update', 'accounting:accounts:deactivate', 'accounting:accounts:delete'] },
    'tok', 't1', 'c1',
  );
});
afterEach(() => {
  mock.restore();
  useAuthStore.getState().clearAuth();
});

const renderPanel = (account = makeAccount()) => {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <AccountDetailsPanel account={account} onEdit={() => undefined} onCreateChild={() => undefined} />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

describe('AccountDetailsPanel — financial constraints', () => {
  it('deletes only when backend accepts and shows constraint list in confirm', async () => {
    mock.onGet('/Accounts/a1/breadcrumb').reply(200, []);
    mock.onDelete('/Accounts/a1').reply(204);
    renderPanel();

    expect(screen.getByTestId('delete-constraints').textContent).toContain('CannotDeleteAccountWithFinancialActivity');
    fireEvent.click(screen.getByTestId('delete-account'));
    fireEvent.click(screen.getByText('حذف قطعی'));
    await waitFor(() => expect(mock.history.delete).toHaveLength(1));
  });

  it('disables delete for accounts with children (backend: CannotDeleteAccountWithChildren)', () => {
    mock.onGet('/Accounts/a1/breadcrumb').reply(200, []);
    renderPanel(makeAccount({ hasChildren: true }));
    expect(screen.getByTestId('delete-account')).toBeDisabled();
    // محدودیت بدون نیاز به confirm، inline و همیشه نمایان است
    expect(screen.getByTestId('delete-constraints').textContent).toContain('CannotDeleteAccountWithChildren');
  });

  it('hides delete for system accounts (backend: CannotDeleteSystemAccount)', () => {
    mock.onGet('/Accounts/a1/breadcrumb').reply(200, []);
    renderPanel(makeAccount({ isSystemAccount: true }));
    expect(screen.queryByTestId('delete-account')).toBeNull();
    expect(screen.getByText('سیستمی')).toBeInTheDocument();
  });

  it('shows backend error message on failed deactivate', async () => {
    mock.onGet('/Accounts/a1/breadcrumb').reply(200, []);
    mock.onPost('/Accounts/a1/deactivate').reply(409, { code: 'Accounting.HasOpenDocuments', detail: 'حساب در اسناد باز استفاده شده است' });
    renderPanel();

    fireEvent.click(screen.getByTestId('deactivate-account'));
    fireEvent.click(screen.getByText('غیرفعال کن'));
    await waitFor(() => expect(mock.history.post.filter((c) => c.url.includes('/deactivate'))).toHaveLength(1));
    await waitFor(() => expect(toast.error).toHaveBeenCalled());
    expect((toast.error as ReturnType<typeof vi.fn>).mock.calls[0][0]).toBe('غیرفعال‌سازی انجام نشد');
    expect((toast.error as ReturnType<typeof vi.fn>).mock.calls[0][1]).toContain('حساب در اسناد باز');
  });

  it('hides edit/delete actions when user lacks permissions', () => {
    useAuthStore.setState({
      user: { userId: 'u1', email: 'a@b.c', fullName: 'کاربر', roles: [], permissions: ['accounting:accounts:read'] },
    });
    mock.onGet('/Accounts/a1/breadcrumb').reply(200, []);
    renderPanel();
    expect(screen.queryByTestId('edit-account')).toBeNull();
    expect(screen.queryByTestId('delete-account')).toBeNull();
    expect(screen.queryByTestId('deactivate-account')).toBeNull();
  });
});
