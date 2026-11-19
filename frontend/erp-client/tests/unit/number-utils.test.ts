import { describe, expect, it } from 'vitest';
import {
  digitsToEnglish,
  digitsToPersian,
  formatNumber,
  formatNumberPersian,
  groupPersian,
  isNumericInput,
  parseNumberInput,
} from '@/shared/utils/number';

describe('number utils', () => {
  it('converts Persian and Arabic digits to English for parsing', () => {
    expect(digitsToEnglish('۱۲۳')).toBe('123');
    expect(digitsToEnglish('٤٥')).toBe('45');
    expect(digitsToEnglish('1۰2')).toBe('102');
  });

  it('converts English digits to Persian for display only', () => {
    expect(digitsToPersian(1404)).toBe('۱۴۰۴');
    expect(digitsToPersian('12,500')).toBe('۱۲,۵۰۰');
  });

  it('parses Persian digits with Persian separators into a real number', () => {
    expect(parseNumberInput('۱٬۲۵۰٬۰۰۰')).toBe(1250000);
    expect(parseNumberInput('۱٫۵')).toBe(1.5);
    expect(parseNumberInput('۱/۵')).toBe(1.5);
    expect(parseNumberInput('1,250,000')).toBe(1250000);
    expect(parseNumberInput('1250000')).toBe(1250000);
  });

  it('parses negative numbers and rejects invalid text', () => {
    expect(parseNumberInput('-2500')).toBe(-2500);
    expect(parseNumberInput('abc')).toBeNull();
    expect(parseNumberInput('')).toBeNull();
    expect(parseNumberInput('-')).toBeNull();
  });

  it('formats very large amounts without precision loss', () => {
    const huge = 9007199254740992; // 2^53 boundary vicinity
    expect(formatNumber(huge)).toBe('9,007,199,254,740,992');
  });

  it('groups Persian display with the Persian thousands separator', () => {
    expect(groupPersian(1250000)).toBe('۱٬۲۵۰٬۰۰۰');
    expect(formatNumberPersian(250000)).toBe('۲۵۰٬۰۰۰');
  });

  it('validates numeric-only input', () => {
    expect(isNumericInput('۱۲۳')).toBe(true);
    expect(isNumericInput('12a3')).toBe(false);
  });
});
