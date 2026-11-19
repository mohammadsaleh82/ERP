/** تنظیمات حسابداری شرکت — منبع: AccountingSettingsDto + UpdateAccountingSettingsRequest */
export type RoundingMode = 'RoundToEven' | 'AwayFromZero' | 'Floor' | 'Ceiling' | string;
export type VoucherNumberingPolicy = 'ContinuousByFiscalYear' | 'MonthlyRestart' | 'DailyRestart' | string;
export type AllowNegativeBalancesPolicy = 'StrictlyForbidden' | 'WarningOnly' | 'Allowed' | string;

export interface AccountingSettingsDto {
  id: string;
  tenantId: string;
  companyId: string;
  defaultCurrencyCode: string;
  calendarType: 'Persian' | 'Gregorian' | string;
  fiscalYearStartMonth: number;
  fiscalYearStartDay: number;
  decimalPlaces: number;
  roundingMode: RoundingMode;
  voucherNumberingPolicy: VoucherNumberingPolicy;
  allowNegativeBalancesPolicy: AllowNegativeBalancesPolicy;
  approvalPolicy: 'DirectPostingAllowed' | 'TwoStepApprovalRequired' | 'ThreeStepApprovalRequired' | string;
  defaultAccountTemplateId: string | null;
  activeFiscalYearId: string | null;
  lockDate: string | null;
  allowUnbalancedDraft: boolean;
  autoNumberingEnabled: boolean;
  requireSeparateApprover: boolean;
  requireSeparatePoster: boolean;
  /** نسخه برای concurrency — الزامی در PUT */
  version: number;
  lastModifiedAtUtc: string | null;
}

export interface UpdateAccountingSettingsRequest {
  defaultCurrencyCode: string;
  calendarType: 'Persian' | 'Gregorian' | string;
  fiscalYearStartMonth: number;
  fiscalYearStartDay: number;
  decimalPlaces: number;
  roundingMode: RoundingMode;
  voucherNumberingPolicy: VoucherNumberingPolicy;
  allowNegativeBalancesPolicy: AllowNegativeBalancesPolicy;
  approvalPolicy: string;
  defaultAccountTemplateId: string | null;
  lockDate: string | null;
  allowUnbalancedDraft: boolean;
  autoNumberingEnabled: boolean;
  requireSeparateApprover: boolean;
  requireSeparatePoster: boolean;
  changeReason: string | null;
}

export interface AccountingSettingsHistoryDto {
  id: string;
  version: number;
  defaultCurrencyCode: string;
  roundingMode: RoundingMode;
  approvalPolicy: string;
  changeReason: string | null;
  changedBy: string | null;
  changedAtUtc: string;
  [key: string]: unknown;
}
