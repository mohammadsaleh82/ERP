/** بازیابی silent نشست در boot + مسیر مستقیم به route محافظت‌شده */
import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import AxiosMockAdapter from 'axios-mock-adapter';
import axios from 'axios';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthGuard } from '@/app/guards/auth-guard';
import { CompanyGuard } from '@/app/guards/company-guard';
import { BootGate } from '@/app/guards/boot-gate';
import { useAuthStore } from '@/core/auth/auth.store';
import { useCompanyStore } from '@/core/company/company.store';

let refreshMock: AxiosMockAdapter;

beforeEach(() => {
  refreshMock = new AxiosMockAdapter(axios, { onNoMatch: 'throwException' });
  useAuthStore.getState().clearAuth();
});
afterEach(() => {
  refreshMock.restore();
});

describe('BootGate (silent session restore)', () => {
  it('restores the session via refresh cookie and profile, then renders children', async () => {
    refreshMock.onPost().reply(200, { accessToken: 'restored-token', tenantId: 't1', companyId: 'c1', permissions: ['accounting:documents:read'] });
    const { apiClient } = await import('@/core/api/client');
    const meMock = new AxiosMockAdapter(apiClient, { onNoMatch: 'throwException' });
    meMock.onGet('/Auth/me').reply(200, {
      userId: 'u1', email: 'a@b.c', activeTenantId: 't1', activeCompanyId: 'c1',
      roles: ['Accountant'], permissions: ['accounting:documents:read'],
    });

    render(
      <BootGate>
        <div>APP-READY</div>
      </BootGate>,
    );

    expect(await screen.findByText('APP-READY')).toBeInTheDocument();
    expect(useAuthStore.getState().isAuthenticated).toBe(true);
    expect(useAuthStore.getState().accessToken).toBe('restored-token');
    meMock.restore();
  });

  it('shows the boot loading state before restore completes', async () => {
    refreshMock.onPost().reply(() => new Promise((resolve) => setTimeout(() => resolve([200, { accessToken: 't', tenantId: 'x', companyId: 'y', permissions: [] }]), 50)));
    const { apiClient } = await import('@/core/api/client');
    const meMock = new AxiosMockAdapter(apiClient, { onNoMatch: 'throwException' });
    meMock.onGet('/Auth/me').reply(200, { userId: 'u1', email: 'a@b.c', activeTenantId: 't1', activeCompanyId: 'c1', roles: [], permissions: [] });

    render(
      <BootGate>
        <div>APP-READY</div>
      </BootGate>,
    );

    expect(screen.getByRole('status')).toBeInTheDocument();
    expect(await screen.findByText('APP-READY')).toBeInTheDocument();
    meMock.restore();
  });
});

describe('protected route guards', () => {
  it('redirects unauthenticated direct access to /login with from state', () => {
    useAuthStore.getState().clearAuth();
    render(
      <MemoryRouter initialEntries={['/accounting/documents']}>
        <Routes>
          <Route element={<AuthGuard />}>
            <Route path="/accounting/*" element={<div>DOCS</div>} />
          </Route>
          <Route path="/login" element={<div>LOGIN-PAGE</div>} />
        </Routes>
      </MemoryRouter>,
    );
    expect(screen.getByText('LOGIN-PAGE')).toBeInTheDocument();
    expect(screen.queryByText('DOCS')).toBeNull();
  });

  it('redirects authenticated user without active company to /select-company', () => {
    useAuthStore.getState().setAuth(
      { userId: 'u1', email: 'a@b.c', fullName: 'کاربر', roles: [], permissions: [] },
      'tok', 't1', 'c1',
    );
    useCompanyStore.getState().clearCompanyContext();

    render(
      <MemoryRouter initialEntries={['/accounting']}>
        <Routes>
          <Route element={<AuthGuard />}>
            <Route element={<CompanyGuard />}>
              <Route path="/accounting" element={<div>ACCOUNTING</div>} />
            </Route>
          </Route>
          <Route path="/select-company" element={<div>SELECT-COMPANY</div>} />
        </Routes>
      </MemoryRouter>,
    );
    expect(screen.getByText('SELECT-COMPANY')).toBeInTheDocument();
    expect(screen.queryByText('ACCOUNTING')).toBeNull();
  });

  it('lets authenticated user with active company through', () => {
    useAuthStore.getState().setAuth(
      { userId: 'u1', email: 'a@b.c', fullName: 'کاربر', roles: [], permissions: [] },
      'tok', 't1', 'c1',
    );
    useCompanyStore.getState().setCompanyContext(
      { id: 't1', name: 'گروه آریا', companies: [{ id: 'c1', name: 'آریا', fiscalYear: 1404 }] },
      { id: 'c1', name: 'آریا', fiscalYear: 1404 },
    );

    render(
      <MemoryRouter initialEntries={['/accounting']}>
        <Routes>
          <Route element={<AuthGuard />}>
            <Route element={<CompanyGuard />}>
              <Route path="/accounting" element={<div>ACCOUNTING</div>} />
            </Route>
          </Route>
        </Routes>
      </MemoryRouter>,
    );
    expect(screen.getByText('ACCOUNTING')).toBeInTheDocument();
  });
});
