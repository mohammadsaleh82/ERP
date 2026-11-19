import type { DocumentStatus, FiscalYearStatus } from '@/core/dashboard/dashboard.types';

/** برچسب فارسی وضعیت‌ها — دقیقاً مطابق enumهای backend */
const documentStatusLabels: Record<DocumentStatus, string> = {
  Draft: 'پیش‌نویس',
  PendingApproval: 'در انتظار تأیید',
  Reviewed: 'بررسی‌شده',
  Approved: 'تأییدشده',
  Posted: 'قطعی‌شده',
  Reversed: 'معکوس‌شده',
};

const fiscalYearStatusLabels: Record<FiscalYearStatus, string> = {
  Draft: 'پیش‌نویس',
  Open: 'باز',
  Closing: 'اختتامیه',
  Closed: 'بسته',
};

export const statusToLabel = (status: DocumentStatus): string => documentStatusLabels[status] ?? status;
export const fiscalStatusToLabel = (status: FiscalYearStatus): string => fiscalYearStatusLabels[status] ?? status;
