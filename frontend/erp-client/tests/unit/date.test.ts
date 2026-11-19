import { describe, expect, it } from 'vitest';
import { formatToPersianDate, formatToPersianDateTime, toIsoUtc } from '@/shared/utils/date';

describe('date utilities', () => {
  it('formats a valid date to Persian (Jalali) calendar', () => {
    const result = formatToPersianDate(new Date('2025-01-05T00:00:00Z'));
    // ۱۴۰۳/۱۰/۱۵ در تقویم شمسی
    expect(result).toMatch(/۱۴۰۳/);
    expect(result).not.toMatch(/—/);
  });

  it('formats a date string input the same as a Date object', () => {
    const fromString = formatToPersianDate('2025-01-05');
    const fromDate = formatToPersianDate(new Date('2025-01-05T00:00:00Z'));
    expect(fromString).toBe(fromDate);
  });

  it('returns the em-dash placeholder for null, undefined and invalid values', () => {
    expect(formatToPersianDate(null)).toBe('—');
    expect(formatToPersianDate(undefined)).toBe('—');
    expect(formatToPersianDate('not-a-date')).toBe('—');
  });

  it('includes the time part in datetime formatting', () => {
    const result = formatToPersianDateTime(new Date('2025-01-05T08:30:00Z'));
    expect(result).toMatch(/۰۸|۰۹|:30|:3۰/);
  });

  it('converts a date to ISO UTC', () => {
    const iso = toIsoUtc(new Date('2025-01-05T08:30:00Z'));
    expect(iso).toBe('2025-01-05T08:30:00.000Z');
  });
});
