import { describe, expect, it } from 'vitest';
import {
  formatDualDate,
  formatGregorian,
  formatJalaliLong,
  formatJalaliRange,
  formatToPersianDate,
} from '@/shared/utils/date';

describe('jalali/gregorian date utils', () => {
  it('formats 2025-01-05 as 1403/10/16 in Persian digits (۱ دی = ۲۱ دسامبر)', () => {
    expect(formatToPersianDate('2025-01-05')).toMatch(/۱۴۰۳/);
    expect(formatToPersianDate('2025-01-05')).toContain('۱۶');
    expect(formatToPersianDate('2025-01-04')).toContain('۱۵');
  });

  it('formats a long Jalali date with the month name (دی)', () => {
    expect(formatJalaliLong('2025-01-05')).toContain('دی');
    expect(formatJalaliLong('2025-01-05')).toContain('۱۴۰۳');
  });

  it('is stable regardless of machine timezone (noon UTC stays the same date)', () => {
    // 12:00 UTC در همه TZهای متداول (−12…+14) همان روز جلالی را می‌دهد
    expect(formatToPersianDate('2025-01-05T12:00:00Z')).toContain('۱۶');
  });

  it('formats the Gregorian counterpart with English digits, LTR-safe', () => {
    expect(formatGregorian('2025-01-05')).toBe('05/01/2025');
  });

  it('combines both calendars for financial documents', () => {
    const dual = formatDualDate('2025-01-05');
    expect(dual).toContain('۱۴۰۳');
    expect(dual).toContain('2025');
  });

  it('renders a Persian range label', () => {
    const range = formatJalaliRange('2025-03-21', '2026-03-20');
    expect(range).toContain('از');
    expect(range).toContain('تا');
  });

  it('falls back to em-dash for invalid/null dates', () => {
    expect(formatDualDate(null)).toBe('—');
    expect(formatGregorian('not-a-date')).toBe('—');
  });
});
