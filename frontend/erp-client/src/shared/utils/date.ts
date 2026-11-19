/**
 * Date utilities for Persian (Jalali) and UTC conversion
 */

export function formatToPersianDate(date: string | Date | null | undefined): string {
  if (!date) return '—';

  const d = typeof date === 'string' ? new Date(date) : date;
  if (isNaN(d.getTime())) return '—';

  return new Intl.DateTimeFormat('fa-IR', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(d);
}

export function formatToPersianDateTime(date: string | Date | null | undefined): string {
  if (!date) return '—';

  const d = typeof date === 'string' ? new Date(date) : date;
  if (isNaN(d.getTime())) return '—';

  return new Intl.DateTimeFormat('fa-IR', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(d);
}

export function toIsoUtc(date: Date): string {
  return date.toISOString();
}

/** نمایش کامل شمسی با نام ماه — «۱۵ دی ۱۴۰۳» */
export function formatJalaliLong(date: string | Date | null | undefined): string {
  if (!date) return '—';
  const d = typeof date === 'string' ? new Date(date) : date;
  if (isNaN(d.getTime())) return '—';
  return new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  }).format(d);
}

/** نمایش میلادی با ارقام انگلیسی و isolate برای متن ترکیبی */
export function formatGregorian(date: string | Date | null | undefined): string {
  if (!date) return '—';
  const d = typeof date === 'string' ? new Date(date) : date;
  if (isNaN(d.getTime())) return '—';
  return new Intl.DateTimeFormat('en-GB', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(d);
}

/** نمایش دوگانه: شمسی + میلادی در پرانتز — برای اسناد مالی */
export function formatDualDate(date: string | Date | null | undefined): string {
  if (!date) return '—';
  const jalali = formatToPersianDate(date);
  const gregorian = formatGregorian(date);
  if (jalali === '—' || gregorian === '—') return jalali;
  return `${jalali} (${gregorian})`;
}

/** نمایش بازه دو تاریخ شمسی — برای دوره مالی/فیلتر گزارش */
export function formatJalaliRange(
  from: string | Date | null | undefined,
  to: string | Date | null | undefined,
): string {
  if (!from && !to) return '—';
  const fromLabel = from ? formatToPersianDate(from) : '…';
  const toLabel = to ? formatToPersianDate(to) : '…';
  return `از ${fromLabel} تا ${toLabel}`;
}
