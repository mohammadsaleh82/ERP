/**
 * انواع دقیق پاسخ Backend — منابع:
 * LedgerDtos.cs (AccountBalanceResultDto) · DocumentDtos.cs (DocumentDto/PagedDocumentsResult)
 * FiscalYearDtos.cs (FiscalYearDto/FiscalPeriodDto) · AccountingAuditTrailService.cs (AuditTrailEntryDto)
 * Pagination.cs (PagedResult)
 */

export type DocumentStatus = 'Draft' | 'PendingApproval' | 'Reviewed' | 'Approved' | 'Posted' | 'Reversed';
export type FiscalYearStatus = 'Draft' | 'Open' | 'Closing' | 'Closed';
export type FiscalPeriodStatus = 'Open' | 'Closed' | 'Adjustment' | string;
export type LedgerBalanceDirection = 'Debit' | 'Credit' | 'Balanced' | string;

export interface AccountBalanceResult {
  accountId: string;
  accountFullCode: string;
  accountName: string;
  normalBalance: string;
  openingBalance: number;
  periodDebit: number;
  periodCredit: number;
  closingBalance: number;
  openingDirection: LedgerBalanceDirection;
  closingDirection: LedgerBalanceDirection;
  openingDisplayBalance: number;
  closingDisplayBalance: number;
}

export interface DocumentDto {
  id: string;
  fiscalYearId: string;
  fiscalPeriodId: string;
  documentNumber: number;
  documentDate: string;
  description: string;
  documentType: string;
  status: DocumentStatus;
  referenceNumber: string | null;
  sourceModule: string | null;
  totalDebit: number;
  totalCredit: number;
  currencyCode: string;
  rejectionReason: string | null;
  createdBy: string | null;
  createdAtUtc: string;
  linesCount: number;
}

export interface PagedDocumentsResult {
  items: DocumentDto[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface FiscalYearDto {
  id: string;
  yearNumber: number;
  name: string;
  startDate: string;
  endDate: string;
  status: FiscalYearStatus;
  isClosed: boolean;
}

export interface FiscalPeriodDto {
  id: string;
  fiscalYearId: string;
  periodNumber: number;
  name: string;
  startDate: string;
  endDate: string;
  status: FiscalPeriodStatus;
  isAdjustmentPeriod: boolean;
}

export interface AuditTrailEntryDto {
  id: string;
  userId: string;
  action: string;
  entityType: string;
  entityId: string;
  timestampUtc: string;
  correlationId: string;
  reason: string | null;
}

export interface AccountDto {
  id: string;
  code: string;
  fullCode: string;
  name: string;
  level: number | string;
  isActive: boolean;
}

export interface PagedAccountsResult {
  items: AccountDto[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}
