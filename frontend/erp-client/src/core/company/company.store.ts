import { create } from 'zustand';

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
  setActiveTenant: (tenant: TenantSummary | null) => void;
  setActiveCompany: (company: CompanySummary | null) => void;
  setActiveBranchId: (branchId: string | null) => void;
  toggleSidebar: () => void;
  toggleTheme: () => void;
}

export const useCompanyStore = create<CompanyState>((set) => ({
  activeTenant: null,
  activeCompany: null,
  activeBranchId: null,
  sidebarCollapsed: false,
  theme: 'light',

  setActiveTenant: (tenant) => set({ activeTenant: tenant }),
  setActiveCompany: (company) => set({ activeCompany: company }),
  setActiveBranchId: (branchId) => set({ activeBranchId: branchId }),
  toggleSidebar: () => set((state) => ({ sidebarCollapsed: !state.sidebarCollapsed })),
  toggleTheme: () =>
    set((state) => {
      const nextTheme = state.theme === 'light' ? 'dark' : 'light';
      if (typeof document !== 'undefined') {
        if (nextTheme === 'dark') {
          document.documentElement.classList.add('dark');
        } else {
          document.documentElement.classList.remove('dark');
        }
      }
      return { theme: nextTheme };
    }),
}));
