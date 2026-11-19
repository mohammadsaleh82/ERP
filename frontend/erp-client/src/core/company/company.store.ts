/* eslint-disable react-refresh/only-export-components */
import { create } from 'zustand';
import type { UserTenantDto } from '@/core/auth/auth.service';

export interface CompanySummary {
  id: string;
  name: string;
  fiscalYear: number;
}

export interface TenantSummary {
  id: string;
  name: string;
  companies: CompanySummary[];
}

interface CompanyState {
  activeTenant: TenantSummary | null;
  activeCompany: CompanySummary | null;
  activeBranchId: string | null;
  sidebarCollapsed: boolean;
  theme: 'light' | 'dark';
  /** شرکت‌های واقعی کاربر از GET /Auth/companies */
  tenants: TenantSummary[];
  setTenants: (dtos: UserTenantDto[]) => void;
  /**
   * تعویض زمینه شرکت — همراه پاک‌سازی cache وابسته به شرکت
   * (queryClient.clear از interceptor caller انجام می‌شود تا نشت داده بین شرکت‌ها رخ ندهد).
   */
  setCompanyContext: (tenant: TenantSummary | null, company: CompanySummary | null) => void;
  clearCompanyContext: () => void;
  setActiveBranchId: (branchId: string | null) => void;
  toggleSidebar: () => void;
  toggleTheme: () => void;
}

const SIDEBAR_KEY = 'erp.sidebarCollapsed';
const THEME_KEY = 'erp.theme';

/** ترجیحات UI (نه داده مالی) در localStorage — بی‌خطر برای امنیت */
const loadSidebar = (): boolean => {
  try {
    return localStorage.getItem(SIDEBAR_KEY) === '1';
  } catch {
    return false;
  }
};

const loadTheme = (): 'light' | 'dark' => {
  try {
    return localStorage.getItem(THEME_KEY) === 'dark' ? 'dark' : 'light';
  } catch {
    return 'light';
  }
};

const applyTheme = (theme: 'light' | 'dark') => {
  if (typeof document !== 'undefined') {
    document.documentElement.classList.toggle('dark', theme === 'dark');
  }
};

const toTenantSummaries = (dtos: UserTenantDto[]): TenantSummary[] =>
  dtos.map((tenant) => ({
    id: tenant.tenantId,
    name: tenant.tenantName,
    companies: tenant.companies.map((company) => ({
      id: company.companyId,
      name: company.name,
      fiscalYear: company.activeFiscalYear,
    })),
  }));

export const useCompanyStore = create<CompanyState>((set, get) => ({
  activeTenant: null,
  activeCompany: null,
  activeBranchId: null,
  sidebarCollapsed: loadSidebar(),
  theme: loadTheme(),
  tenants: [],

  setTenants: (dtos) => set({ tenants: toTenantSummaries(dtos) }),

  setCompanyContext: (tenant, company) => {
    set({ activeTenant: tenant, activeCompany: company, activeBranchId: null });
  },

  clearCompanyContext: () =>
    set({ activeTenant: null, activeCompany: null, activeBranchId: null }),

  setActiveBranchId: (branchId) => set({ activeBranchId: branchId }),

  toggleSidebar: () =>
    set((state) => {
      const next = !state.sidebarCollapsed;
      try {
        localStorage.setItem(SIDEBAR_KEY, next ? '1' : '0');
      } catch {
        /* storage در دسترس نیست */
      }
      return { sidebarCollapsed: next };
    }),

  toggleTheme: () => {
    const next: 'light' | 'dark' = get().theme === 'light' ? 'dark' : 'light';
    applyTheme(next);
    try {
      localStorage.setItem(THEME_KEY, next);
    } catch {
      /* storage در دسترس نیست */
    }
    set({ theme: next });
  },
}));

// اعمال تم ذخیره‌شده در اولین بارگذاری
applyTheme(useCompanyStore.getState().theme);
