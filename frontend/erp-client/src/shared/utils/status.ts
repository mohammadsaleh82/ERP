/**
 * نگاشت وضعیت‌های دامنه مالی به برچسب/رنگ — منبع واحد حقیقت
 * برای VoucherStatusBadge و FiscalPeriodStatusBadge.
 */

export type VoucherStatus = 'draft' | 'pending' | 'posted' | 'reversed';
export type FiscalPeriodStatus = 'open' | 'closed' | 'adjusting';

export interface StatusDescriptor {
  label: string;
  /** نام رنگ Token — با کلاس‌های bg/text ترکیب می‌شود */
  tone: 'neutral' | 'warning' | 'success' | 'info' | 'primary';
}

export const VOUCHER_STATUS_MAP: Record<VoucherStatus, StatusDescriptor> = {
  draft: { label: 'پیش‌نویس', tone: 'neutral' },
  pending: { label: 'در انتظار تأیید', tone: 'warning' },
  posted: { label: 'قطعی شده', tone: 'success' },
  reversed: { label: 'ابطال/برگشتی', tone: 'info' },
};

export const FISCAL_PERIOD_STATUS_MAP: Record<FiscalPeriodStatus, StatusDescriptor> = {
  open: { label: 'باز', tone: 'success' },
  closed: { label: 'بسته', tone: 'primary' },
  adjusting: { label: 'در حال تعدیل', tone: 'warning' },
};

/** برچسب امن برای وضعیت نامشخص از بک‌اند */
export function voucherStatusLabel(status: string): StatusDescriptor {
  return VOUCHER_STATUS_MAP[status as VoucherStatus] ?? { label: status, tone: 'neutral' };
}

export function fiscalPeriodStatusLabel(status: string): StatusDescriptor {
  return FISCAL_PERIOD_STATUS_MAP[status as FiscalPeriodStatus] ?? { label: status, tone: 'neutral' };
}
