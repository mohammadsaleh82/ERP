/**
 * PermissionGuard — دروازه مجوز سمت UI: allow (رندر children)، deny (AccessDeniedPage).
 * منبع حقیقت مجوزها: permissions از backend (auth store) — نه از رنگ/محلی.
 */
import { describe, expect, it, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { PermissionGuard } from '@/app/guards/permission-guard';
import { useAuthStore } from '@/core/auth/auth.store';

const renderAt = (path: string, element: React.ReactNode, initial: string) =>
  render(
    <MemoryRouter initialEntries={[initial]}>
      <Routes>
        <Route path={path} element={element} />
        <Route path="/access-denied" element={<div data-testid="denied">دسترسی مجاز نیست</div>} />
        <Route path="*" element={<div data-testid="not-found">404</div>} />
      </Routes>
    </MemoryRouter>,
  );

describe('PermissionGuard', () => {
  beforeEach(() => {
    useAuthStore.getState().clearAuth();
  });

  it('با داشتن یکی از مجوزهای خواسته‌شده children رندر می‌شود', () => {
    useAuthStore.getState().setAuth(
      { userId: 'u1', email: 'a@b.c', fullName: 'حسابدار', roles: [], permissions: ['accounting:documents:create'] },
      'tok', 't1', 'c1',
    );
    renderAt(
      '/guarded',
      <PermissionGuard permissions={['accounting:documents:create', 'accounting:documents:submit']}>
        <div data-testid="content">محتوای محافظت‌شده</div>
      </PermissionGuard>,
      '/guarded',
    );
    expect(screen.getByTestId('content')).toBeInTheDocument();
    expect(screen.queryByTestId('denied')).not.toBeInTheDocument();
  });

  it('بدون مجوز → AccessDeniedPage رندر می‌شود، نه children', () => {
    useAuthStore.getState().setAuth(
      { userId: 'u1', email: 'a@b.c', fullName: 'کاربر فاقد مجوز', roles: [], permissions: ['payroll:salaries:view'] },
      'tok', 't1', 'c1',
    );
    renderAt(
      '/guarded',
      <PermissionGuard permissions={['accounting:documents:create']}>
        <div data-testid="content">محتوای محافظت‌شده</div>
      </PermissionGuard>,
      '/guarded',
    );
    expect(screen.queryByTestId('content')).not.toBeInTheDocument();
    expect(screen.getByTestId('denied')).toBeInTheDocument();
  });

  it('کاربر بدون هیچ مجوزی (لیست خالی) هم deny می‌شود', () => {
    useAuthStore.getState().setAuth(
      { userId: 'u1', email: 'a@b.c', fullName: 'مهمان', roles: [], permissions: [] },
      'tok', 't1', 'c1',
    );
    renderAt(
      '/guarded',
      <PermissionGuard permissions={['accounting:accounts:read']}>
        <div data-testid="content">x</div>
      </PermissionGuard>,
      '/guarded',
    );
    expect(screen.getByTestId('denied')).toBeInTheDocument();
  });
});
