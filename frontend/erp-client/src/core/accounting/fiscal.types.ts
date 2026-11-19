/** انواع سال/دوره مالی — منبع: FiscalYearDtos.cs + AccountingEnums.cs */
export type FiscalCalendarType = 'Persian' | 'Gregorian' | string;
export type FiscalYearStatus = 'Draft' | 'Open' | 'Closing' | 'Closed' | string;
export type FiscalPeriodStatus = 'Draft' | 'Open' | 'Locked' | 'Closed' | string;
export type PeriodGenerationType = 'Monthly' | 'Quarterly' | 'Custom' | string;

export interface FiscalYearDto {
  id: string;
  tenantId: string;
  companyId: string;
  yearNumber: number;
  name: string;
  startDate: string;
  endDate: string;
  calendarType: FiscalCalendarType;
  status: FiscalYearStatus;
  isClosed: boolean;
  openingBalancePosted: boolean;
  /** هر فیلد اضافه‌ای که Backend اضافه کند */
  [key: string]: unknown;
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

export interface CreateFiscalYearRequest {
  yearNumber: number;
  name: string;
  calendarType: FiscalCalendarType;
  startDate?: string | null;
  endDate?: string | null;
  autoGeneratePeriods: PeriodGenerationType | null;
}

export interface GeneratePeriodsRequest {
  generationType: PeriodGenerationType;
}

export interface CreateCustomPeriodRequest {
  periodNumber: number;
  name: string;
  startDate: string;
  endDate: string;
  isAdjustmentPeriod: boolean;
}
