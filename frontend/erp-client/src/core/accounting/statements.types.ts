/**
 * انواع صورت‌های مالی — منبع واقعی: ReportDtos.cs (ReportDtos لایه Application)
 * ترازنامه/سود و زیان/جریان نقد/بدهکاران و بستانکاران/سنی + طبقه‌بندی‌ها.
 * مبالغ به رشته decimal برای نمایش تمام‌دقیق.
 */
export type CashFlowCategoryName = 'Operating' | 'Investing' | 'Financing';

/** شماره‌های enum واقعی backend — برای PUT طبقه‌بندی */
export const CASH_FLOW_CATEGORY: Record<CashFlowCategoryName, number> = {
  Operating: 1,
  Investing: 2,
  Financing: 3,
};

export const CASH_FLOW_CATEGORY_LABELS: Record<CashFlowCategoryName, string> = {
  Operating: 'فعالیت عملیاتی',
  Investing: 'فعالیت سرمایه‌گذاری',
  Financing: 'فعالیت تأمین مالی',
};

export type ReportCategoryName =
  | 'CurrentAsset' | 'NonCurrentAsset' | 'AccountsReceivable' | 'CashAndCashEquivalents'
  | 'CurrentLiability' | 'NonCurrentLiability' | 'AccountsPayable' | 'Equity'
  | 'Revenue' | 'CostOfGoodsSold' | 'OperatingExpense' | 'TaxExpense' | 'OtherIncome' | 'OtherExpense';

export const REPORT_CATEGORY_LABELS: Record<ReportCategoryName, string> = {
  CurrentAsset: 'دارایی جاری',
  NonCurrentAsset: 'دارایی غیرجاری',
  AccountsReceivable: 'حساب دریافتنی (بدهکاران)',
  CashAndCashEquivalents: 'وجوه نقد و معادل‌ها',
  CurrentLiability: 'بدهی جاری',
  NonCurrentLiability: 'بدهی غیرجاری',
  AccountsPayable: 'حساب پرداختنی (بستانکاران)',
  Equity: 'حقوق مالکانه',
  Revenue: 'درآمد عملیاتی',
  CostOfGoodsSold: 'بهای تمام‌شده فروش',
  OperatingExpense: 'هزینه عملیاتی',
  TaxExpense: 'مالیات',
  OtherIncome: 'درآمد غیرعملیاتی',
  OtherExpense: 'هزینه غیرعملیاتی',
};

export interface StatementRowDto {
  accountId: string;
  accountCode: string;
  accountName: string;
  balance: string;
  category: ReportCategoryName;
}

export interface StatementSectionDto {
  title: string;
  rows: StatementRowDto[];
  total: string;
}

export interface BalanceSheetDto {
  companyId: string;
  asOfDate: string;
  currentAssets: StatementSectionDto;
  nonCurrentAssets: StatementSectionDto;
  totalAssets: string;
  currentLiabilities: StatementSectionDto;
  nonCurrentLiabilities: StatementSectionDto;
  totalLiabilities: string;
  equity: StatementSectionDto;
  currentPeriodResult: string;
  totalEquity: string;
  isBalanced: boolean;
  difference: string;
}

export interface ProfitLossDto {
  companyId: string;
  fromDate: string;
  toDate: string;
  revenues: StatementSectionDto;
  costOfGoodsSold: StatementSectionDto;
  grossProfit: string;
  operatingExpenses: StatementSectionDto;
  operatingProfit: string;
  otherIncome: StatementSectionDto;
  otherExpenses: StatementSectionDto;
  profitBeforeTax: string;
  taxExpenses: StatementSectionDto;
  netProfit: string;
}

export interface CashFlowBucketDto {
  category: CashFlowCategoryName;
  inflow: string;
  outflow: string;
  net: string;
}

export interface CashFlowDto {
  companyId: string;
  fromDate: string;
  toDate: string;
  buckets: CashFlowBucketDto[];
  openingCash: string;
  closingCash: string;
  cashAccountsConfigured: boolean;
  note: string | null;
}

export interface PartyBalanceRowDto {
  dimensionId: string;
  dimensionLabel: string;
  openingDebit: string;
  openingCredit: string;
  debitTurnover: string;
  creditTurnover: string;
  closingDebit: string;
  closingCredit: string;
  signedBalance: string;
}

export interface PartyLedgerResultDto {
  title: string;
  accountsConfigured: boolean;
  note: string | null;
  rows: PartyBalanceRowDto[];
  totalDebit: string;
  totalCredit: string;
}

export interface AgingBucketDto {
  label: string;
  amount: string;
}

export interface AgingResultDto {
  title: string;
  hasReliableDueDates: boolean;
  note: string;
  asOfDate: string;
  buckets: AgingBucketDto[];
  totalAmount: string;
}

export interface ReportingClassificationDto {
  id: string;
  rootCode: string;
  category: ReportCategoryName;
}

export interface CashFlowClassificationDto {
  id: string;
  accountId: string;
  category: CashFlowCategoryName;
}

/** فرمت‌های خروجی — ReportExportFormat واقعی backend */
export type ReportExportFormatName = 'Excel' | 'Pdf' | 'Csv';

/** نام گزارش‌های خروجی‌پذیر — دقیقاً مطابق switch کنترلر */
export const EXPORTABLE_REPORTS = ['trialbalance', 'journal', 'generalledger', 'balancesheet', 'profitloss', 'cashflow', 'receivables', 'payables', 'partyturnover', 'accountcard'] as const;
export type ExportableReportName = (typeof EXPORTABLE_REPORTS)[number];
