/** تغییر شرکت — بدون نشت داده بین شرکت‌ها در cache فرانت */
import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import AxiosMockAdapter from 'axios-mock-adapter';
import axios from 'axios';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { apiClient } from '@/core/api/client';
import { useAuthStore } from '@/core/auth/auth.store';
import { useCompanyStore } from '@/core/company/company.store';
import { queryClient } from '@/app/providers/query-provider';
import { RequirePermission } from '@/shared/components/authorization/require-permission';
import { PERMISSIONS } from '@/core/auth/permissions';

let mock: AxiosMockAdapter;
let refreshMock: AxiosMockAdapter;

const TOKEN_COMPANY_A = 'token-company-a';
const TOKEN_COMPANY_B = 'token-company-b';

beforeEach(() => {
  mock = new AxiosMockAdapter(apiClient, { onNoMatch: 'throwException' });
  refreshMock = new AxiosMockAdapter(axios, { onNoMatch: 'throwException' });
  useAuthStore.getState().setAuth(
    { userId: 'u1', email: 'a@b.c', fullName: 'کاربر', roles: [], permissions: ['accounting:documents:read'] },
    TOKEN_COMPANY_A, 't1', 'cA',
  );
  useCompanyStore.getState().setTenants([
    { tenantId: 't1', tenantName: 'گروه آریا', companies: [
      { companyId: 'cA', name: 'بازرگانی آریا', activeFiscalYear: 1404, isActive: true, membershipActive: true },
      { companyId: 'cB', name: 'صنایع پارس', activeFiscalYear: 1403, isActive: true, membershipActive: true },
    ] },
  ]);
  useCompanyStore.getState().setCompanyContext(
    { id: 't1', name: 'گروه آریا', companies: [{ id: 'cA', name: 'بازرگانی آریا', fiscalYear: 1404 }] },
    { id: 'cA', name: 'بازرگانی آریا', fiscalYear: 1404 },
  );
  queryClient.setQueryData(['fiscal-years', 'cA'], [{ id: 'fy1', title: '۱۴۰۴' }]);
});
afterEach(() => {
  mock.restore();
  refreshMock.restore();
});

describe('company switch', () => {
  it('refreshes with new context headers and clears the query cache of the old company', async () => {
    // cache داده شرکت A
    expect(queryClient.getQueryData(['fiscal-years', 'cA'])).toBeTruthy();

    const refreshSpy = vi.fn((config: { headers?: Record<string, string> }) => {
      // backend membership validation سمت هدر — اینجا شبیه‌سازی پاسخ موفق
      expect(config.headers?.['X-Tenant-Id']).toBe('t1');
      expect(config.headers?.['X-Company-Id']).toBe('cB');
      return [200, { accessToken: TOKEN_COMPANY_B, tenantId: 't1', companyId: 'cB', permissions: ['accounting:documents:read'] }];
    });
    refreshMock.onPost().reply(refreshSpy);

    const { refreshSession } = await import('@/core/auth/auth.service');
    const session = await refreshSession({ tenantId: 't1', companyId: 'cB' });

    useAuthStore.getState().setSession(session.accessToken, session.tenantId, session.companyId, session.permissions);
    useCompanyStore.getState().setCompanyContext(
      { id: 't1', name: 'گروه آریا', companies: [{ id: 'cB', name: 'صنایع پارس', fiscalYear: 1403 }] },
      { id: 'cB', name: 'صنایع پارس', fiscalYear: 1403 },
    );
    // همان کاری که Header/CompanySelection انجام می‌دهند:
    queryClient.clear();

    expect(useAuthStore.getState().accessToken).toBe(TOKEN_COMPANY_B);
    expect(useAuthStore.getState().activeCompanyId).toBe('cB');
    expect(queryClient.getQueryData(['fiscal-years', 'cA'])).toBeUndefined();
    // درخواست بعدی هدر شرکت جدید را می‌فرستد
    let seenCompanyHeader: string | null = null;
    mock.onGet('/Reports/summary').reply((config) => {
      seenCompanyHeader = String(config.headers?.['X-Company-Id'] ?? '');
      return [200, {}];
    });
    await apiClient.get('/Reports/summary');
    expect(seenCompanyHeader).toBe('cB');
    expect(refreshSpy).toHaveBeenCalledTimes(1);
  });

  it('rejects switching to a company without active membership (403 from refresh)', async () => {
    refreshMock.onPost().reply(403, { code: 'Auth.CompanyAccessDenied', detail: 'کاربر عضو فعال این شرکت نیست.' });
    const { refreshSession } = await import('@/core/auth/auth.service');

    await expect(refreshSession({ tenantId: 't1', companyId: 'cNo' })).rejects.toMatchObject({
      status: 403,
      code: 'Auth.CompanyAccessDenied',
    });
    // نشست قبلی دست‌نخورده می‌ماند
    expect(useAuthStore.getState().activeCompanyId).toBe('cA');
  });
});

describe('permission-gated UI', () => {
  it('hides button without the required permission and disables in disable mode', () => {
    useAuthStore.setState({
      user: { userId: 'u1', email: 'a@b.c', fullName: 'کاربر', roles: [], permissions: ['accounting:documents:read'] },
    });

    render(
      <div>
        <RequirePermission permissions={[PERMISSIONS.documentsPost]}>
          <button data-testid="hidden-post-btn">قطعی‌سازی سند (hide)</button>
        </RequirePermission>
        <RequirePermission permissions={[PERMISSIONS.documentsPost]} mode="disable">
          <button data-testid="post-btn">قطعی‌سازی سند (disable)</button>
        </RequirePermission>
        <RequirePermission permissions={[PERMISSIONS.documentsRead]}>
          <button data-testid="view-btn">مشاهده اسناد</button>
        </RequirePermission>
      </div>,
    );

    expect(screen.queryByTestId('hidden-post-btn')).toBeNull(); // hide mode → رندر نمی‌شود
    expect(screen.getByTestId('post-btn')).toBeDisabled(); // disable mode → غیرفعال نمایش داده می‌شود
    expect(screen.getByTestId('view-btn')).toBeEnabled(); // مجاز
  });
});
