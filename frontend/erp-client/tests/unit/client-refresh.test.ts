/**
 * سناریوهای حیاتی نشست: refresh هم‌زمان، انقضا، مسیر refresh دوم (ضد-حلقه).
 * Refresh واقعی با axios خام و Cookie HttpOnly انجام می‌شود؛ اینجا فقط endpoint آن mock می‌شود.
 */
import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import AxiosMockAdapter from 'axios-mock-adapter';
import axios from 'axios';
import { apiClient } from '@/core/api/client';
import { useAuthStore } from '@/core/auth/auth.store';
import { useCompanyStore } from '@/core/company/company.store';
import { queryClient } from '@/app/providers/query-provider';

let mock: AxiosMockAdapter;
let refreshMock: AxiosMockAdapter;

const oldToken = (suffix: string) => `old-${suffix}`;
const newToken = (suffix: string) => `new-${suffix}`;

beforeEach(() => {
  mock = new AxiosMockAdapter(apiClient, { onNoMatch: 'throwException' });
  // refresh با axios مستقل (همان‌طور که در client.ts) — instance جدا
  refreshMock = new AxiosMockAdapter(axios, { onNoMatch: 'throwException' });
  useAuthStore.getState().setAuth(
    { userId: 'u1', email: 'a@b.c', fullName: 'کاربر', roles: [], permissions: ['accounting:documents:read'] },
    oldToken('base'), 't1', 'c1',
  );
  useCompanyStore.getState().setCompanyContext(
    { id: 't1', name: 'گروه آریا', companies: [{ id: 'c1', name: 'بازرگانی', fiscalYear: 1404 }] },
    { id: 'c1', name: 'بازرگانی', fiscalYear: 1404 },
  );
  queryClient.clear();
});
afterEach(() => {
  mock.restore();
  refreshMock.restore();
});

describe('session expiry & concurrent refresh', () => {
  it('refreshes exactly once for multiple simultaneous 401s and retries all with the new token', async () => {
    // دو endpoint که با توکن کهنه 401 می‌دهند
    mock.onGet('/Reports/summary').reply((config) => {
      return config.headers?.Authorization === `Bearer ${newToken('0')}` ? [200, { ok: 'r' }] : [401];
    });
    mock.onGet('/Accounts/tree').reply((config) => {
      return config.headers?.Authorization === `Bearer ${newToken('0')}` ? [200, { ok: 'a' }] : [401];
    });
    const refreshSpy = vi.fn(() => [200, { accessToken: newToken('0'), tenantId: 't1', companyId: 'c1', permissions: ['accounting:documents:read'] }]);
    refreshMock.onPost().reply(refreshSpy);

    const [r1, r2] = await Promise.all([
      apiClient.get('/Reports/summary'),
      apiClient.get('/Accounts/tree'),
    ]);

    expect(r1.data.ok).toBe('r');
    expect(r2.data.ok).toBe('a');
    // جلوگیری از درخواست‌های تکراری هنگام Refresh: دقیقا یک فراخوانی
    expect(refreshSpy).toHaveBeenCalledTimes(1);
    expect(useAuthStore.getState().accessToken).toBe(newToken('0'));
  });

  it('terminates the session when refresh is expired (401 SessionExpired) and clears company context', async () => {
    mock.onGet('/Reports/summary').reply(401);
    refreshMock.onPost().reply(401, { code: 'Auth.SessionExpired', detail: 'نشست کاری شما منقضی یا نامعتبر شده است.' });

    await expect(apiClient.get('/Reports/summary')).rejects.toMatchObject({ code: 'Auth.SessionExpired' });
    expect(useAuthStore.getState().isAuthenticated).toBe(false);
    expect(useAuthStore.getState().accessToken).toBeNull();
    expect(useCompanyStore.getState().activeCompany).toBeNull();
    // cache پاک شده — بدون نشت داده به نشست بعدی
    expect(queryClient.getQueryData(['fiscal-years', 'c1'])).toBeUndefined();
  });

  it('never retries the same request twice (no infinite 401 loop)', async () => {
    // حتی بعد از refresh موفق، endpoint همچنان 401 می‌دهد → فقط یک retry مجاز
    const endpointCalls = vi.fn(() => [401]);
    mock.onGet('/Reports/summary').reply(endpointCalls);
    refreshMock.onPost().reply(200, { accessToken: newToken('loop'), tenantId: 't1', companyId: 'c1', permissions: [] });

    await expect(apiClient.get('/Reports/summary')).rejects.toBeTruthy();
    expect(endpointCalls).toHaveBeenCalledTimes(2); // initial + one retry
  });

  it('maps 403 responses to Auth.Forbidden without retry', async () => {
    mock.onGet('/Ledger/balances').reply(403, { code: 'Auth.Forbidden', detail: 'شما به این بخش دسترسی ندارید.' });
    await expect(apiClient.get('/Ledger/balances')).rejects.toMatchObject({ status: 403, code: 'Auth.Forbidden' });
  });
});
