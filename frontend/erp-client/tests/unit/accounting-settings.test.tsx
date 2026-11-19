/** تنظیمات حسابداری: فیلدهای واقعی، PUT با نسخه+ChangeReason، تعارض 409، مجوزها */
import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import AxiosMockAdapter from 'axios-mock-adapter';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { apiClient } from '@/core/api/client';
import { useAuthStore } from '@/core/auth/auth.store';
import { AccountingSettingsPage } from '@/features/accounting/pages/accounting-settings-page';
import { toast } from '@/core/notifications/toast';

vi.mock('@/core/notifications/toast', () => ({
  toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() },
}));

let mock: AxiosMockAdapter;

const settings = {
  id: 's1', tenantId: 't1', companyId: 'c1', defaultCurrencyCode: 'IRR', calendarType: 'Persian',
  fiscalYearStartMonth: 1, fiscalYearStartDay: 1, decimalPlaces: 0, roundingMode: 'RoundToEven',
  voucherNumberingPolicy: 'ContinuousByFiscalYear', allowNegativeBalancesPolicy: 'StrictlyForbidden',
  approvalPolicy: 'ThreeStepApprovalRequired', defaultAccountTemplateId: null, activeFiscalYearId: 'fy1',
  lockDate: null, allowUnbalancedDraft: false, autoNumberingEnabled: true,
  requireSeparateApprover: true, requireSeparatePoster: false, version: 7, lastModifiedAtUtc: '2026-10-01T10:00:00Z',
};

beforeEach(() => {
  mock = new AxiosMockAdapter(apiClient, { onNoMatch: 'throwException' });
  useAuthStore.getState().setAuth(
    { userId: 'u1', email: 'a@b.c', fullName: 'مدیر', roles: [], permissions: ['accounting:settings:read', 'accounting:settings:update'] },
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
        <AccountingSettingsPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

describe('AccountingSettingsPage', () => {
  it('renders all real fields from GET /accounting/settings', async () => {
    mock.onGet('/accounting/settings').reply(200, settings);
    renderPage();

    expect(await screen.findByTestId('settings-currency')).toHaveValue('IRR');
    expect(screen.getByTestId('settings-decimal')).toHaveValue('0');
    expect(screen.getByTestId('settings-rounding')).toHaveValue('RoundToEven');
    expect(screen.getByTestId('settings-calendar')).toHaveValue('Persian');
    expect(screen.getByTestId('settings-approval')).toHaveValue('ThreeStepApprovalRequired');
    expect(screen.getByTestId('settings-numbering')).toHaveValue('ContinuousByFiscalYear');
    expect(screen.getByTestId('accounting-settings-page').textContent).toContain('نسخه فعلی:');
    expect(screen.getByTestId('accounting-settings-page').textContent).toContain('۷');
    expect(screen.getByTestId('settings-sep-approver')).toHaveAttribute('aria-checked', 'true');
  });

  it('PUTs real UpdateAccountingSettingsRequest with version fields and change reason', async () => {
    mock.onGet('/accounting/settings').reply(200, settings);
    mock.onPut('/accounting/settings').reply(200, { ...settings, version: 8, roundingMode: 'AwayFromZero' });
    renderPage();

    fireEvent.change(await screen.findByTestId('settings-rounding'), { target: { value: 'AwayFromZero' } });
    fireEvent.change(screen.getByTestId('settings-change-reason'), { target: { value: 'تصویب هیئت‌مدیره' } });
    fireEvent.click(screen.getByTestId('settings-save'));

    await waitFor(() => expect(mock.history.put).toHaveLength(1));
    const body = JSON.parse(mock.history.put[0].data);
    expect(body).toMatchObject({
      defaultCurrencyCode: 'IRR',
      roundingMode: 'AwayFromZero',
      approvalPolicy: 'ThreeStepApprovalRequired',
      requireSeparateApprover: true,
      changeReason: 'تصویب هیئت‌مدیره',
    });
    await waitFor(() => expect(toast.success).toHaveBeenCalled());
    expect((toast.success as ReturnType<typeof vi.fn>).mock.calls[0][1]).toContain('نسخه ۸');
  });

  it('handles 409 version conflict with refresh message', async () => {
    mock.onGet('/accounting/settings').reply(200, settings);
    mock.onPut('/accounting/settings').reply(409, { code: 'Concurrency.Conflict', detail: 'نسخه تغییر کرده است' });
    renderPage();

    fireEvent.click(await screen.findByTestId('settings-save'));
    await waitFor(() => expect(mock.history.put).toHaveLength(1));
    // بعد از conflict، فرم دوباره بارگذاری می‌شود
    await waitFor(() => expect(mock.history.get.filter((c) => c.url.includes('/accounting/settings')).length).toBeGreaterThanOrEqual(2));
  });

  it('shows history entries from GET /settings/history', async () => {
    mock.onGet('/accounting/settings').reply(200, settings);
    mock.onGet('/accounting/settings/history').reply(200, [
      { id: 'h1', version: 6, defaultCurrencyCode: 'IRR', roundingMode: 'Floor', approvalPolicy: 'TwoStepApprovalRequired', changeReason: 'اصلاح سیاست گرد کردن', changedBy: 'user-99', changedAtUtc: '2026-09-01T08:00:00Z' },
    ]);
    renderPage();

    fireEvent.click(await screen.findByTestId('open-history'));
    expect(await screen.findByText('اصلاح سیاست گرد کردن')).toBeInTheDocument();
    expect(screen.getByText('نسخه ۶')).toBeInTheDocument();
  });

  it('read-only without settings:update (save card hidden)', async () => {
    useAuthStore.setState({
      user: { userId: 'u1', email: 'a@b.c', fullName: 'کاربر', roles: [], permissions: ['accounting:settings:read'] },
    });
    mock.onGet('/accounting/settings').reply(200, settings);
    renderPage();

    expect(await screen.findByText('برای تغییر تنظیمات مجوز accounting:settings:update لازم است.')).toBeInTheDocument();
    expect(screen.queryByTestId('settings-save')).toBeNull();
  });

  it('blocks page entirely without settings:read', () => {
    useAuthStore.setState({
      user: { userId: 'u1', email: 'a@b.c', fullName: 'کاربر', roles: [], permissions: [] },
    });
    renderPage();
    expect(screen.getByText('مجوز مشاهده تنظیمات را ندارید')).toBeInTheDocument();
    expect(mock.history.get.filter((call) => call.url.includes('/accounting/settings'))).toHaveLength(0);
  });
});
