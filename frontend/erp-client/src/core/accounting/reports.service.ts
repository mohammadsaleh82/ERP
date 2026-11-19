/**
 * سرویس دفاتر و گزارش‌ها — endpointهای واقعی:
 * /accounting/ledger (ViewLedger) و /accounting/reports (ViewFinancialReports)
 * و /accounting/dimensions/analysis (dimensions:read).
 * پارامترها دقیقاً مطابق signature کنترلرها؛ مبالغ به رشته decimal برای نمایش تمام‌دقیق.
 */
import { apiClient } from '@/core/api/client';
import type {
  AccountBalanceResultDto,
  AccountCardResultDto,
  AccountTurnoverDto,
  CombinedDimensionReportDto,
  DimensionTurnoverReportDto,
  FinancialDimensionDto,
  GeneralLedgerReportDto,
  GeneralLedgerRowDto,
  JournalReportDto,
  JournalRowDto,
  LedgerTransactionsReportDto,
  PagedResultDto,
  SubsidiaryLedgerReportDto,
  SubsidiaryLedgerRowDto,
  TrialBalanceResultDto,
} from './reports.types';

/** تبدیل یک‌باره پارامترهای undefined → حذف از query */
const params = <T extends object>(filter: T): Record<string, string | number | boolean> => {
  const clean: Record<string, string | number | boolean> = {};
  for (const [key, value] of Object.entries(filter) as Array<[string, unknown]>) {
    if (typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean') clean[key] = value;
  }
  return clean;
};

/* ── LedgerController: /accounting/ledger ─────────────────────────── */

/** دفتر روزنامه — JournalQueryRequest (fromDate/toDate) + PagedRequest */
export async function getLedgerJournal(filter: {
  fiscalYearId?: string;
  fromDate?: string;
  toDate?: string;
  documentNumber?: number;
  accountId?: string;
  pageNumber?: number;
  pageSize?: number;
}): Promise<JournalReportDto> {
  const { data } = await apiClient.get<JournalReportDto>('/accounting/ledger/journal', { params: params(filter) });
  return data;
}

/** دفتر کل — خلاصه گردش/مانده حساب‌ها، صفحه‌بندی واقعی */
export async function getLedgerGeneral(filter: {
  accountId?: string;
  fiscalYearId?: string;
  fromDate?: string;
  toDate?: string;
  includeNonPostingAccounts?: boolean;
  pageNumber?: number;
  pageSize?: number;
}): Promise<GeneralLedgerReportDto> {
  const { data } = await apiClient.get<GeneralLedgerReportDto>('/accounting/ledger/general-ledger', { params: params(filter) });
  return data;
}

/** گردش سطر به سطر یک حساب در دفتر کل */
export async function getLedgerTransactions(accountId: string, filter: {
  fiscalYearId?: string;
  fromDate?: string;
  toDate?: string;
  pageNumber?: number;
  pageSize?: number;
}): Promise<LedgerTransactionsReportDto> {
  const { data } = await apiClient.get<LedgerTransactionsReportDto>(`/accounting/ledger/general-ledger/accounts/${accountId}/transactions`, { params: params(filter) });
  return data;
}

/** دفتر معین — حساب کل/معین والد */
export async function getLedgerSubsidiary(filter: {
  parentAccountId: string;
  fiscalYearId?: string;
  fromDate?: string;
  toDate?: string;
}): Promise<SubsidiaryLedgerReportDto> {
  const { data } = await apiClient.get<SubsidiaryLedgerReportDto>('/accounting/ledger/subsidiary-ledger', { params: params(filter) });
  return data;
}

/** موتور مانده — Opening/PeriodDebit/Credit/Closing با جهت و پرچم غیرعادی */
export async function getLedgerBalances(filter: {
  accountId?: string;
  fiscalYearId?: string;
  periodId?: string;
  fromDate?: string;
  toDate?: string;
}): Promise<AccountBalanceResultDto[]> {
  const { data } = await apiClient.get<AccountBalanceResultDto[]>('/accounting/ledger/balances', { params: params(filter) });
  return data ?? [];
}

/* ── ReportsController: /accounting/reports (from/to) ─────────────── */

export interface ReportFilterParams {
  fiscalYearId?: string;
  fiscalPeriodId?: string;
  from?: string;
  to?: string;
  accountId?: string;
  accountCodePrefix?: string;
  accountLevel?: string;
  partyId?: string;
  detailId?: string;
  costCenterId?: string;
  projectId?: string;
  currencyCode?: string;
  level?: string;
  page?: number;
  pageSize?: number;
}

export async function getTrialBalance(filter: ReportFilterParams): Promise<TrialBalanceResultDto> {
  const { data } = await apiClient.get<TrialBalanceResultDto>('/accounting/reports/trial-balance', { params: params(filter) });
  return data;
}

export async function getReportsJournal(filter: ReportFilterParams): Promise<PagedResultDto<JournalRowDto>> {
  const { data } = await apiClient.get<PagedResultDto<JournalRowDto>>('/accounting/reports/journal', { params: params(filter) });
  return data;
}

export async function getReportsGeneralLedger(filter: ReportFilterParams): Promise<GeneralLedgerRowDto[]> {
  const { data } = await apiClient.get<GeneralLedgerRowDto[]>('/accounting/reports/general-ledger', { params: params(filter) });
  return data ?? [];
}

export async function getReportsSubsidiaryLedger(filter: ReportFilterParams): Promise<PagedResultDto<SubsidiaryLedgerRowDto>> {
  const { data } = await apiClient.get<PagedResultDto<SubsidiaryLedgerRowDto>>('/accounting/reports/subsidiary-ledger', { params: params(filter) });
  return data;
}

/** کارت حساب — مانده اول، ردیف‌ها با مانده تراکمی، مانده پایان (صفحه‌بندی‌شده) */
export async function getAccountCard(accountId: string, filter: ReportFilterParams): Promise<AccountCardResultDto> {
  const { data } = await apiClient.get<AccountCardResultDto>(`/accounting/reports/account-card/${accountId}`, { params: params(filter) });
  return data;
}

export async function getAccountTurnover(accountId: string, filter: ReportFilterParams): Promise<AccountTurnoverDto> {
  const { data } = await apiClient.get<AccountTurnoverDto>(`/accounting/reports/account-turnover/${accountId}`, { params: params(filter) });
  return data;
}

/* ── ابعاد مالی: /accounting/dimensions ───────────────────────────── */

/** فهرست ابعاد مالی واقعی (تفصیلی شناور/مرکز هزینه/پروژه/شخص و ...) */
export async function getFinancialDimensions(filter: {
  dimensionType?: number;
  isActive?: boolean;
  searchTerm?: string;
  pageNumber?: number;
  pageSize?: number;
}): Promise<{ items: FinancialDimensionDto[]; totalCount: number }> {
  const { data } = await apiClient.get<{ items: FinancialDimensionDto[]; totalCount: number; pageNumber: number; pageSize: number }>('/accounting/dimensions', { params: params(filter) });
  return { items: data?.items ?? [], totalCount: data?.totalCount ?? 0 };
}

/** گردش به تفکیک نوع بعد مالی (DimensionTurnoverQuery — PagedRequest با pageNumber/pageSize) */
export async function getDimensionTurnover(filter: {
  dimensionType: number;
  dimensionId?: string;
  fiscalYearId?: string;
  fromDate?: string;
  toDate?: string;
  pageNumber?: number;
  pageSize?: number;
}): Promise<DimensionTurnoverReportDto> {
  const { data } = await apiClient.get<DimensionTurnoverReportDto>('/accounting/dimensions/analysis/turnover', { params: params(filter) });
  return data;
}

/** گزارش ترکیبی چندبعدی — dimensionTypes به‌صورت کلید تکرارشونده (بدون []) */
export async function getCombinedDimensionAnalysis(filter: {
  dimensionTypes: number[];
  accountId?: string;
  fiscalYearId?: string;
  fromDate?: string;
  toDate?: string;
  maxRows?: number;
}): Promise<CombinedDimensionReportDto> {
  const search = new URLSearchParams();
  for (const type of filter.dimensionTypes) search.append('dimensionTypes', String(type));
  for (const [key, value] of Object.entries(params({ ...filter, dimensionTypes: undefined }))) {
    search.append(key, String(value));
  }
  // کلیدهای تکرارشونده در query string واقعی URL (قرارداد binding ASP.NET)
  const { data } = await apiClient.get<CombinedDimensionReportDto>(`/accounting/dimensions/analysis/combined?${search.toString()}`);
  return data;
}
