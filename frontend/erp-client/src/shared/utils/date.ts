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
