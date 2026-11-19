/** decimal-safe: parse ورودی فارسی، جمع بدون float، فرمت، round-trip guard */
import { describe, expect, it } from 'vitest';
import { parseAmountInput, formatAmount, sumAmounts, toApiNumber, computeTotals } from '@/core/accounting/documents.service';

describe('amount helpers (decimal-safe)', () => {
  it('parses Persian/Arabic digits and separators without float', () => {
    expect(parseAmountInput('۱۲٬۵۰۰')).toBe('12500');
    expect(parseAmountInput('1,250.5')).toBe('1250.5');
    expect(parseAmountInput('١٢٣٫٤٥')).toBe('123.45');
    expect(parseAmountInput('')).toBe('');
    expect(parseAmountInput('abc')).toBe('NaN');
    expect(parseAmountInput('12.500.5')).toBe('NaN');
  });

  it('sums exactly where float would drift (0.1+0.2)', () => {
    expect(sumAmounts(['0.1', '0.2'])).toBe('0.3');
    expect(sumAmounts(['999999999999999', '0.1'])).toBe('999999999999999.1');
    expect(sumAmounts([])).toBe('0');
  });

  it('computes balanced and unbalanced totals with difference', () => {
    const balanced = computeTotals([
      { key: 'a', side: 'Debit', amount: '1500.75', amountRaw: '1500.75' } as never,
      { key: 'b', side: 'Credit', amount: '1000.75', amountRaw: '' } as never,
      { key: 'c', side: 'Credit', amount: '500', amountRaw: '' } as never,
    ]);
    expect(balanced.balanced).toBe(true);
    expect(balanced.difference).toBe('0');
    const unbalanced = computeTotals([
      { key: 'a', side: 'Debit', amount: '100', amountRaw: '100' } as never,
      { key: 'b', side: 'Credit', amount: '99.99', amountRaw: '' } as never,
    ]);
    expect(unbalanced.difference).toBe('0.01');
    expect(unbalanced.balanced).toBe(false);
  });

  it('formats with thousand separators and hides zero as dash', () => {
    expect(formatAmount('1234567')).toBe('1,234,567');
    expect(formatAmount('0')).toBe('—');
    expect(formatAmount(null)).toBe('—');
  });

  it('round-trip guard: exact decimal survives Number conversion; rejects beyond precision', () => {
    expect(toApiNumber('1234.56')).toBe(1234.56);
    expect(toApiNumber('999999999999999')).toBe(999999999999999);
    expect(() => toApiNumber('12345678901234567890.123')).toThrow(/دقت/);
  });
});
