/** سرویس داده داشبورد — فقط endpointهای واقعی، بدون ساخت عدد */
import { apiClient } from '@/core/api/client';
import { useCompanyStore } from '@/core/company/company.store';
import type {
  AccountBalanceResult,
  AuditTrailEntryDto,
  DocumentStatus,
  FiscalPeriodDto,
  FiscalYearDto,
  PagedAccountsResult,
  PagedDocumentsResult,
} from './dashboard.types';

export interface DateRange {
  /** ISO yyyy-MM-dd */
  from?: string;
  to?: string;
}

export interface DashboardRangeQuery {
  fiscalYearId?: string;
  from?: string;
  to?: string;
}

export async function getLedgerBalances(range: DashboardRangeQuery): Promise<AccountBalanceResult[]> {
  const { data } = await apiClient.get<AccountBalanceResult[]>('/Ledger/balances', { params: range });
  return data ?? [];
}

export async function searchDocuments(params: {
  status?: DocumentStatus;
  fromDate?: string;
  toDate?: string;
  documentNumber?: number;
  pageSize?: number;
}): Promise<PagedDocumentsResult> {
  const { data } = await apiClient.get<PagedDocumentsResult>('/Documents', { params });
  return data ?? { items: [], totalCount: 0, pageNumber: 1, pageSize: params.pageSize ?? 20 };
}

export async function getFiscalYears(): Promise<FiscalYearDto[]> {
  const { data } = await apiClient.get<FiscalYearDto[]>('/FiscalYears');
  return data ?? [];
}

export async function getFiscalPeriods(fiscalYearId: string): Promise<FiscalPeriodDto[]> {
  const { data } = await apiClient.get<FiscalPeriodDto[]>(`/FiscalYears/${fiscalYearId}/periods`);
  return data ?? [];
}

export async function getRecentAuditTrail(pageSize = 8): Promise<AuditTrailEntryDto[]> {
  const { data } = await apiClient.get<{ items: AuditTrailEntryDto[] }>('/AuditTrail', {
    params: { page: 1, pageSize },
  });
  return data?.items ?? [];
}

/** جست‌وجوی حساب با SearchTerm واقعی (کد/نام) */
export async function searchAccounts(searchTerm: string, pageSize = 10): Promise<PagedAccountsResult> {
  const { data } = await apiClient.get<PagedAccountsResult>('/Accounts', {
    params: { searchTerm, pageSize },
  });
  return data ?? { items: [], totalCount: 0, pageNumber: 1, pageSize };
}

/** بازه پیش‌فرض: ابتدای سال مالی فعال تا امروز (مبنای همه widgets) */
export function defaultRange(): DateRange {
  const { activeCompany } = useCompanyStore.getState();
  const today = new Date();
  const year = activeCompany?.fiscalYear ?? today.getFullYear();
  // ابتدای سال مالی بر اساس تقویم شمسی سال فعال — تا مشخص شدن قرارداد سال مالی دقیق، از نیمه شمسی تقریبی استفاده نمی‌کنیم؛ بازه «سال میلادی جاری» امن و بدون ساخت داده است.
  const from = new Date(Date.UTC(year, 0, 1)).toISOString().slice(0, 10);
  const to = today.toISOString().slice(0, 10);
  return { from, to };
}
