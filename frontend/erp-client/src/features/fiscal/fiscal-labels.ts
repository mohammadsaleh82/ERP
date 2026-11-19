/** برچسب‌های فارسی سال/دوره مالی — مطابق AccountingEnums.cs */
import type { FiscalCalendarType, FiscalPeriodStatus, FiscalYearStatus, PeriodGenerationType } from '@/core/accounting/fiscal.types';

export const fiscalCalendarLabels: Record<string, string> = {
  Persian: 'هجری شمسی',
  Gregorian: 'میلادی',
};

export const fiscalYearStatusLabels: Record<string, string> = {
  Draft: 'پیش‌نویس',
  Open: 'باز',
  Closing: 'در اختتامیه',
  Closed: 'بسته قطعی',
};

export const fiscalPeriodStatusLabels: Record<string, string> = {
  Draft: 'پیش‌نویس',
  Open: 'باز',
  Locked: 'قفل موقت',
  Closed: 'بسته قطعی',
};

export const periodGenerationLabels: Record<string, string> = {
  Monthly: 'ماهانه (۱۲ دوره)',
  Quarterly: 'فصلی (۴ دوره)',
  Custom: 'سفارشی دستی',
};

export const calendarLabel = (type: FiscalCalendarType): string => fiscalCalendarLabels[type] ?? String(type);
export const yearStatusLabel = (status: FiscalYearStatus): string => fiscalYearStatusLabels[status] ?? String(status);
export const periodStatusLabel = (status: FiscalPeriodStatus): string => fiscalPeriodStatusLabels[status] ?? String(status);
export const generationLabel = (type: PeriodGenerationType): string => periodGenerationLabels[type] ?? String(type);
