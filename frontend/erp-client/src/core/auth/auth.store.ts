import { create } from 'zustand';
import { SYSTEM_ADMIN } from './permissions';

/** مطابق پاسخ واقعی GET /api/v1/Auth/me و LoginResponse */
export interface UserProfile {
  userId: string;
  email: string;
  fullName: string;
  roles: string[];
  permissions: string[];
}

interface AuthState {
  user: UserProfile | null;
  accessToken: string | null;
  /** زمینه فعال نشست — هم‌زمان با JWT نگه داشته می‌شود */
  activeTenantId: string | null;
  activeCompanyId: string | null;
  isAuthenticated: boolean;
  setAuth: (user: UserProfile, accessToken: string, tenantId: string, companyId: string) => void;
  /** پس از refresh: توکن و زمینه جدید — user بدون تغییر */
  setSession: (accessToken: string, tenantId: string, companyId: string, permissions: string[]) => void;
  setAccessToken: (token: string) => void;
  clearAuth: () => void;
  hasPermission: (permission: string) => boolean;
  hasAnyPermission: (permissions: readonly string[]) => boolean;
  hasAllPermissions: (permissions: readonly string[]) => boolean;
  isSuperAdmin: () => boolean;
}

export const useAuthStore = create<AuthState>((set, get) => ({
  user: null,
  accessToken: null,
  activeTenantId: null,
  activeCompanyId: null,
  isAuthenticated: false,

  setAuth: (user, accessToken, tenantId, companyId) =>
    set({
      user,
      accessToken,
      activeTenantId: tenantId,
      activeCompanyId: companyId,
      isAuthenticated: true,
    }),

  setSession: (accessToken, tenantId, companyId, permissions) =>
    set((state) => ({
      accessToken,
      activeTenantId: tenantId,
      activeCompanyId: companyId,
      user: state.user ? { ...state.user, permissions } : null,
      isAuthenticated: true,
    })),

  setAccessToken: (accessToken) => set({ accessToken }),

  clearAuth: () =>
    set({
      user: null,
      accessToken: null,
      activeTenantId: null,
      activeCompanyId: null,
      isAuthenticated: false,
    }),

  hasPermission: (permission) => {
    const { user } = get();
    if (!user) return false;
    return user.permissions.includes(permission) || user.permissions.includes(SYSTEM_ADMIN);
  },

  hasAnyPermission: (permissions) => {
    const { user } = get();
    if (!user) return false;
    if (user.permissions.includes(SYSTEM_ADMIN)) return true;
    return permissions.some((permission) => user.permissions.includes(permission));
  },

  hasAllPermissions: (permissions) => {
    const { user } = get();
    if (!user) return false;
    if (user.permissions.includes(SYSTEM_ADMIN)) return true;
    return permissions.every((permission) => user.permissions.includes(permission));
  },

  isSuperAdmin: () => get().user?.permissions.includes(SYSTEM_ADMIN) ?? false,
}));
