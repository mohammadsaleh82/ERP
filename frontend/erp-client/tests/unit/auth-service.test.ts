import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import AxiosMockAdapter from 'axios-mock-adapter';
import { apiClient } from '@/core/api/client';
import { login, logout, getCurrentUser, getUserCompanies } from '@/core/auth/auth.service';
import { useAuthStore } from '@/core/auth/auth.store';
import { useCompanyStore } from '@/core/company/company.store';
import { queryClient } from '@/app/providers/query-provider';

let mock: AxiosMockAdapter;

const token = () => 'a'.repeat(20) + '.' + 'b'.repeat(20) + '.' + 'c'.repeat(20);


beforeEach(() => {
  mock = new AxiosMockAdapter(apiClient, { onNoMatch: 'throwException' });
  useAuthStore.getState().clearAuth();
  useCompanyStore.getState().clearCompanyContext();
  queryClient.clear();
});
afterEach(() => mock.restore());

describe('login (POST /Auth/login)', () => {
  it('succeeds and stores session with tenant/company context', async () => {
    mock.onPost('/Auth/login').reply(200, {
      accessToken: token(),
      email: 'admin@erp.local',
      fullName: 'مدیر سیستم',
      tenantId: '11111111-1111-1111-1111-111111111111',
      companyId: '22222222-2222-2222-2222-222222222222',
      permissions: ['accounting:documents:read'],
    });

    const response = await login({ email: 'admin@erp.local', password: 'secret' });
    expect(response.permissions).toContain('accounting:documents:read');
  });

  it('fails with the backend Persian security message (401)', async () => {
    mock.onPost('/Auth/login').reply(401, { code: 'Auth.InvalidCredentials', detail: 'نام کاربری یا کلمه عبور اشتباه است.' });

    await expect(login({ email: 'admin@erp.local', password: 'wrong' })).rejects.toMatchObject({
      status: 401,
      code: 'Auth.InvalidCredentials',
      message: 'نام کاربری یا کلمه عبور اشتباه است.',
    });
    expect(useAuthStore.getState().isAuthenticated).toBe(false);
  });
});

describe('logout (POST /Auth/logout)', () => {
  it('revokes server-side and always clears local auth', async () => {
    useAuthStore.getState().setAuth(
      { userId: 'u1', email: 'a@b.c', fullName: 'کاربر', roles: [], permissions: [] },
      token(), 't1', 'c1',
    );
    mock.onPost('/Auth/logout').reply(200, { message: 'خروج با موفقیت انجام شد.' });

    await logout();
    expect(useAuthStore.getState().isAuthenticated).toBe(false);
    expect(useAuthStore.getState().accessToken).toBeNull();
    expect(useCompanyStore.getState().activeCompany).toBeNull();
  });

  it('clears local state even when the revoke request fails', async () => {
    useAuthStore.getState().setAuth(
      { userId: 'u1', email: 'a@b.c', fullName: 'کاربر', roles: [], permissions: [] },
      token(), 't1', 'c1',
    );
    mock.onPost('/Auth/logout').networkError();

    await expect(logout()).rejects.toBeTruthy();
    expect(useAuthStore.getState().isAuthenticated).toBe(false);
  });
});

describe('getCurrentUser (GET /Auth/me)', () => {
  it('maps the me contract to UserProfile', async () => {
    mock.onGet('/Auth/me').reply(200, {
      userId: 'u1',
      email: 'a@b.c',
      activeTenantId: 't1',
      activeCompanyId: 'c1',
      roles: ['Accountant'],
      permissions: ['accounting:documents:read', 'accounting:documents:create'],
    });
    const profile = await getCurrentUser();
    expect(profile.roles).toEqual(['Accountant']);
    expect(profile.permissions).toHaveLength(2);
  });
});

describe('getUserCompanies (GET /Auth/companies)', () => {
  it('returns real tenant/company memberships', async () => {
    mock.onGet('/Auth/companies').reply(200, [
      {
        tenantId: 't1',
        tenantName: 'گروه آریا',
        companies: [{ companyId: 'c1', name: 'بازرگانی آریا', activeFiscalYear: 1404, isActive: true, membershipActive: true }],
      },
    ]);
    const tenants = await getUserCompanies();
    expect(tenants).toHaveLength(1);
    expect(tenants[0].companies[0].activeFiscalYear).toBe(1404);
  });
});
