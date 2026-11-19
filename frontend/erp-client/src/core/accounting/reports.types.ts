/**
 * انواع دفاتر و گزارش‌های مالی — منبع واقعی: LedgerDtos.cs + ReportDtos.cs +
 * DimensionAnalysisService.cs. مبالغ از سرور به رشته decimal تبدیل می‌شوند
 * (نمایش تمام‌دقیق با decimal.js).
 */
export type LedgerBalanceDirection = 'None' | 'Debtor' | 'Creditor';
export type NormalBalanceSide = 'Debit' | 'Credit';
export type AccountLevelName = 'Group' | 'General' | 'Subsidiary' | 'Detail';
export type FinancialDimensionTypeName = 'DetailAccount' | 'CostCenter' | 'Project' | 'Branch' | 'Contract' | 'Party' | 'Customer';

/** شماره‌های enum واقعی backend — برای binding کوئری */
export const DIMENSION_TYPE: Record<FinancialDimensionTypeName, number> = {
  DetailAccount: 1,
  CostCenter: 2,
  Project: 3,
  Branch: 4,
  Contract: 5,
  Party: 6,
  Customer: 7,
};

/* ── دفتر روزنامه (ledger) ─────────────────────────────────────────── */

export interface JournalLineDto {
  accountId: string;
  accountFullCode: string;
  accountName: string;
  lineNumber: number;
  description: string;
  debit: string;
  credit: string;
}

export interface JournalEntryDto {
  documentId: string;
  documentNumber: number;
  documentDate: string;
  description: string;
  sourceModule: string | null;
  totalDebit: string;
  totalCredit: string;
  lines: JournalLineDto[];
}

export interface DailyJournalTotalDto {
  date: string;
  documentsCount: number;
  totalDebit: string;
  totalCredit: string;
}

export interface JournalReportDto {
  entries: JournalEntryDto[];
  dailyTotals: DailyJournalTotalDto[];
  periodTotalDebit: string;
  periodTotalCredit: string;
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

/* ── دفتر کل (ledger) ──────────────────────────────────────────────── */

export interface AccountLedgerSummaryDto {
  accountId: string;
  accountFullCode: string;
  accountName: string;
  normalBalance: NormalBalanceSide;
  openingBalance: string;
  periodDebit: string;
  periodCredit: string;
  closingBalance: string;
  closingDirection: LedgerBalanceDirection;
  closingDisplayBalance: string;
  isAbnormalClosingBalance: boolean;
}

export interface GeneralLedgerReportDto {
  fiscalYearId: string | null;
  fromDate: string;
  toDate: string;
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPeriodDebit: string;
  totalPeriodCredit: string;
  accounts: AccountLedgerSummaryDto[];
}

export interface LedgerTransactionDto {
  entryId: string;
  documentId: string;
  documentNumber: number;
  lineNumber: number;
  entryDate: string;
  description: string;
  debit: string;
  credit: string;
  currencyCode: string;
  sourceModule: string | null;
  sourceDocumentId: string | null;
}

export interface LedgerTransactionsReportDto {
  accountId: string;
  accountFullCode: string;
  accountName: string;
  openingBalance: string;
  totalDebit: string;
  totalCredit: string;
  closingBalance: string;
  closingDirection: LedgerBalanceDirection;
  closingDisplayBalance: string;
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  transactions: LedgerTransactionDto[];
}

/* ── دفتر معین ─────────────────────────────────────────────────────── */

export interface SubsidiaryLedgerReportDto {
  parentAccountId: string;
  parentAccountFullCode: string;
  parentAccountName: string;
  fromDate: string;
  toDate: string;
  subsidiaryAccounts: AccountLedgerSummaryDto[];
  totalOpeningBalance: string;
  totalPeriodDebit: string;
  totalPeriodCredit: string;
  totalClosingBalance: string;
}

/* ── موتور مانده ───────────────────────────────────────────────────── */

export interface AccountBalanceResultDto {
  accountId: string;
  accountFullCode: string;
  accountName: string;
  normalBalance: NormalBalanceSide;
  openingBalance: string;
  periodDebit: string;
  periodCredit: string;
  closingBalance: string;
  openingDirection: LedgerBalanceDirection;
  closingDirection: LedgerBalanceDirection;
  openingDisplayBalance: string;
  closingDisplayBalance: string;
  isAbnormalOpeningBalance: boolean;
  isAbnormalClosingBalance: boolean;
}

/* ── گزارش‌ها (reports) ────────────────────────────────────────────── */

export interface TrialBalanceRow {
  accountId: string | null;
  accountCode: string;
  accountName: string;
  level: AccountLevelName;
  openingDebit: string;
  openingCredit: string;
  periodDebit: string;
  periodCredit: string;
  closingDebit: string;
  closingCredit: string;
}

export interface TrialBalanceResultDto {
  companyId: string;
  fiscalYearName: string | null;
  fromDate: string;
  toDate: string;
  aggregationLevel: AccountLevelName;
  isBalanced: boolean;
  totalDebit: string;
  totalCredit: string;
  difference: string;
  rows: TrialBalanceRow[];
}

export interface JournalRowDto {
  documentNumber: number;
  entryDate: string;
  accountId: string;
  accountCode: string;
  accountName: string;
  description: string;
  debit: string;
  credit: string;
  currencyCode: string;
}

export interface PagedResultDto<T> {
  rows: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface GeneralLedgerRowDto {
  accountId: string;
  accountCode: string;
  accountName: string;
  openingDebit: string;
  openingCredit: string;
  debitTurnover: string;
  creditTurnover: string;
  closingDebit: string;
  closingCredit: string;
}

export interface SubsidiaryLedgerRowDto {
  accountId: string;
  accountCode: string;
  accountName: string;
  documentNumber: number;
  entryDate: string;
  description: string;
  debit: string;
  credit: string;
  currencyCode: string;
}

export interface AccountCardRowDto {
  accountId: string;
  accountCode: string;
  accountName: string;
  entryDate: string;
  documentNumber: number;
  description: string;
  debit: string;
  credit: string;
  runningBalance: string;
  balanceSide: string;
}

export interface AccountCardResultDto {
  accountId: string;
  accountCode: string;
  accountName: string;
  fromDate: string;
  toDate: string;
  openingBalance: string;
  openingSide: string;
  rows: AccountCardRowDto[];
  closingBalance: string;
  closingSide: string;
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface AccountTurnoverDto {
  accountId: string;
  accountCode: string;
  accountName: string;
  openingDebit: string;
  openingCredit: string;
  debitTurnover: string;
  creditTurnover: string;
  closingDebit: string;
  closingCredit: string;
}

/* ── تحلیل ابعاد مالی ──────────────────────────────────────────────── */

export interface FinancialDimensionDto {
  id: string;
  dimensionType: FinancialDimensionTypeName;
  code: string;
  name: string;
  externalReferenceId: string | null;
  ownerModule: string | null;
  isActive: boolean;
}

export interface DimensionTurnoverRowDto {
  dimensionId: string | null;
  dimensionCode: string;
  dimensionName: string;
  debit: string;
  credit: string;
  signedBalance: string;
  direction: LedgerBalanceDirection;
  displayBalance: string;
}

export interface DimensionTurnoverReportDto {
  dimensionType: FinancialDimensionTypeName;
  accountId: string | null;
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalDebit: string;
  totalCredit: string;
  rows: DimensionTurnoverRowDto[];
}

export interface DimensionRefDto {
  dimensionType: FinancialDimensionTypeName;
  dimensionId: string | null;
  code: string;
  name: string;
}

export interface CombinedDimensionRowDto {
  dimensions: DimensionRefDto[];
  debit: string;
  credit: string;
  signedBalance: string;
  direction: LedgerBalanceDirection;
  displayBalance: string;
}

export interface CombinedDimensionReportDto {
  dimensionTypes: FinancialDimensionTypeName[];
  totalMatchedRows: number;
  isTruncated: boolean;
  totalDebit: string;
  totalCredit: string;
  rows: CombinedDimensionRowDto[];
}
