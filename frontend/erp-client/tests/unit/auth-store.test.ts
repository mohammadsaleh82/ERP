import { beforeEach, describe, expect, it } from 'vitest';
import { useAuthStore, type UserProfile } from '@/core/auth/auth.store';

const sampleUser: UserProfile = {
  id: 'u-1',
  email: 'admin@erp.local',
  fullName: 'مدیر سیستم',
  roles: ['Accounting.Manager'],
  permissions: ['accounting:vouchers:view', 'accounting:vouchers:post'],
};

describe('auth store', () => {
  beforeEach(() => {
    useAuthStore.getState().clearAuth();
  });

  it('starts unauthenticated', () => {
    const state = useAuthStore.getState();
    expect(state.isAuthenticated).toBe(false);
    expect(state.user).toBeNull();
    expect(state.accessToken).toBeNull();
  });

  it('setAuth marks the user authenticated and stores the token', () => {
    useAuthStore.getState().setAuth(sampleUser, 'token-123');
    const state = useAuthStore.getState();
    expect(state.isAuthenticated).toBe(true);
    expect(state.user?.fullName).toBe('مدیر سیستم');
    expect(state.accessToken).toBe('token-123');
  });

  it('clearAuth resets every auth field', () => {
    useAuthStore.getState().setAuth(sampleUser, 'token-123');
    useAuthStore.getState().clearAuth();
    const state = useAuthStore.getState();
    expect(state.isAuthenticated).toBe(false);
    expect(state.user).toBeNull();
    expect(state.accessToken).toBeNull();
  });

  it('hasPermission accepts exact permissions but not unrelated ones', () => {
    useAuthStore.getState().setAuth(sampleUser, 'token-123');
    expect(useAuthStore.getState().hasPermission('accounting:vouchers:post')).toBe(true);
    expect(useAuthStore.getState().hasPermission('payroll:salaries:view')).toBe(false);
  });

  it('grants system admins every permission', () => {
    useAuthStore.getState().setAuth({ ...sampleUser, permissions: ['system:admin'] }, 't');
    expect(useAuthStore.getState().hasPermission('anything:at:all')).toBe(true);
  });

  it('denies permissions for anonymous users', () => {
    expect(useAuthStore.getState().hasPermission('accounting:vouchers:view')).toBe(false);
  });

  it('setAccessToken rotates only the token', () => {
    useAuthStore.getState().setAuth(sampleUser, 'old-token');
    useAuthStore.getState().setAccessToken('new-token');
    const state = useAuthStore.getState();
    expect(state.accessToken).toBe('new-token');
    expect(state.isAuthenticated).toBe(true);
    expect(state.user?.id).toBe('u-1');
  });
});
