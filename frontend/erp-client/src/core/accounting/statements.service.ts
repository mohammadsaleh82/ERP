/**
 * سرویس صورت‌های مالی — endpointهای واقعی ReportsController
 * (/accounting/reports/*) با permission accounting:reports:read.
 * همه محاسبات نهایی سمت backend؛ frontend فقط نمایش.
 * خروجی Excel/PDF/CSV هم سمت سرور (POST /reports/export) — منطبق با فیلترها.
 */
import { apiClient } from '@/core/api/client';
import type {
  AgingResultDto,
  BalanceSheetDto,
  CashFlowCategoryName,
  CashFlowClassificationDto,
  CashFlowDto,
  PartyLedgerResultDto,
  ProfitLossDto,
  ReportingClassificationDto,
  ReportExportFormatName,
  ExportableReportName,
} from './statements.types';
import { CASH_FLOW_CATEGORY } from './statements.types';

interface StatementFilterParams {
  fiscalYearId?: string;
  fiscalPeriodId?: string;
  costCenterId?: string;
  projectId?: string;
  currencyCode?: string;
}

const params = <T extends object>(filter: T): Record<string, string | number | boolean> => {
  const clean: Record<string, string | number | boolean> = {};
  for (const [key, value] of Object.entries(filter) as Array<[string, unknown]>) {
    if (typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean') clean[key] = value;
  }
  return clean;
};

/** ترازنامه — تا تاریخ (from به سرور ارسال نمی‌شود: قرارداد endpoint) */
export async function getBalanceSheet(filter: StatementFilterParams & { to?: string }): Promise<BalanceSheetDto> {
  const { data } = await apiClient.get<BalanceSheetDto>('/accounting/reports/balance-sheet', { params: params(filter) });
  return data;
}

/** سود و زیان بازه */
export async function getProfitLoss(filter: StatementFilterParams & { from?: string; to?: string }): Promise<ProfitLossDto> {
  const { data } = await apiClient.get<ProfitLossDto>('/accounting/reports/profit-loss', { params: params(filter) });
  return data;
}

/** جریان وجوه نقد روش مستقیم — فقط طبقه‌بندی‌های backend */
export async function getCashFlow(filter: { fiscalYearId?: string; fiscalPeriodId?: string; from?: string; to?: string }): Promise<CashFlowDto> {
  const { data } = await apiClient.get<CashFlowDto>('/accounting/reports/cash-flow', { params: params(filter) });
  return data;
}

/** بدهکاران — مانده و گردش مشتریان روی حساب‌های دریافتنی */
export async function getReceivables(filter: { fiscalYearId?: string; fiscalPeriodId?: string; from?: string; to?: string }): Promise<PartyLedgerResultDto> {
  const { data } = await apiClient.get<PartyLedgerResultDto>('/accounting/reports/receivables', { params: params(filter) });
  return data;
}

/** بستانکاران — مانده و گردش تأمین‌کنندگان روی حساب‌های پرداختنی */
export async function getPayables(filter: { fiscalYearId?: string; fiscalPeriodId?: string; from?: string; to?: string }): Promise<PartyLedgerResultDto> {
  const { data } = await apiClient.get<PartyLedgerResultDto>('/accounting/reports/payables', { params: params(filter) });
  return data;
}

/** گردش اشخاص روی کل حساب‌ها */
export async function getPartyTurnover(filter: { fiscalYearId?: string; fiscalPeriodId?: string; from?: string; to?: string }): Promise<PartyLedgerResultDto> {
  const { data } = await apiClient.get<PartyLedgerResultDto>('/accounting/reports/party-turnover', { params: params(filter) });
  return data;
}

/** گزارش سنی مطالبات — فقط تا تاریخ (قرارداد endpoint) */
export async function getAging(to?: string): Promise<AgingResultDto> {
  const { data } = await apiClient.get<AgingResultDto>('/accounting/reports/aging', { params: params({ to }) });
  return data;
}

/* ── طبقه‌بندی‌ها ──────────────────────────────────────────────────── */

export async function getReportingClassifications(): Promise<ReportingClassificationDto[]> {
  const { data } = await apiClient.get<ReportingClassificationDto[]>('/accounting/reports/classifications');
  return data ?? [];
}

export async function getCashFlowClassifications(): Promise<CashFlowClassificationDto[]> {
  const { data } = await apiClient.get<CashFlowClassificationDto[]>('/accounting/reports/cash-flow-classifications');
  return data ?? [];
}

export async function upsertCashFlowClassification(accountId: string, category: CashFlowCategoryName): Promise<CashFlowClassificationDto> {
  const { data } = await apiClient.put<CashFlowClassificationDto>('/accounting/reports/cash-flow-classifications', {
    accountId,
    category: CASH_FLOW_CATEGORY[category],
  });
  return data;
}

/* ── خروجی سمت سرور (Excel/PDF/CSV) ───────────────────────────────── */

export interface ReportExportRequest {
  reportName: ExportableReportName;
  format: ReportExportFormatName;
  fiscalYearId?: string;
  fiscalPeriodId?: string;
  from?: string;
  to?: string;
  accountId?: string;
  accountLevel?: string;
  partyId?: string;
  costCenterId?: string;
  projectId?: string;
  currencyCode?: string;
}

/**
 * دریافت فایل خروجی از سرور (همان محاسبات گزارش JSON — تطابق کامل) و ذخیره با
 * نام فایل از Content-Disposition. برای فایل‌های بزرگ محاسبه سمت سرور است.
 */
export async function downloadReportExport(request: ReportExportRequest): Promise<{ fileName: string; blob: Blob }> {
  const response = await apiClient.post<Blob>('/accounting/reports/export', {
    ReportName: request.reportName,
    Format: request.format,
    FiscalYearId: request.fiscalYearId,
    FiscalPeriodId: request.fiscalPeriodId,
    From: request.from,
    To: request.to,
    AccountId: request.accountId,
    AccountLevel: request.accountLevel,
    PartyId: request.partyId,
    CostCenterId: request.costCenterId,
    ProjectId: request.projectId,
    CurrencyCode: request.currencyCode,
  }, { responseType: 'blob' });

  const disposition = String(response.headers['content-disposition'] ?? '');
  const match = /filename\*?=(?:UTF-8''|")?([^";]+)/i.exec(disposition);
  const fileName = match ? decodeURIComponent(match[1].replace(/"/g, '')) : `${request.reportName}.${request.format === 'Excel' ? 'xlsx' : request.format.toLowerCase()}`;
  return { fileName, blob: response.data };
}

/** ذخیره فایل در مرورگر */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}
