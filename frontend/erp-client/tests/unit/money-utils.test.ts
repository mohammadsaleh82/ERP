import { describe, expect, it } from 'vitest';
import {
  currencyLabel,
  formatMoney,
  isNegativeMoney,
  moneyParts,
  parseMoneyInput,
  roundMoney,
} from '@/shared/utils/money';

describe('money utils', () => {
  it('formats amounts with Persian digits, grouping and currency', () => {
    expect(formatMoney(1250000, { withCurrency: true })).toBe('۱٬۲۵۰٬۰۰۰ ریال');
    expect(formatMoney(1250000)).toBe('۱٬۲۵۰٬۰۰۰');
  });

  it('keeps the stored value untouched: parse is inverse of English format', () => {
    const stored = 987654321;
    const english = formatMoney(stored, { persianDigits: false });
    expect(parseMoneyInput(english)).toBe(stored);
  });

  it('formats negative amounts with a proper minus sign', () => {
    expect(formatMoney(-75000)).toBe('−۷۵٬۰۰۰');
    expect(isNegativeMoney(-75000)).toBe(true);
    expect(isNegativeMoney(500)).toBe(false);
  });

  it('renders zero as an em-dash when requested', () => {
    expect(formatMoney(0, { zeroAsDash: true })).toBe('—');
    expect(formatMoney(0)).toBe('۰');
  });

  it('splits money into parts for colored rendering', () => {
    expect(moneyParts(-2500)).toEqual({
      sign: '−',
      absoluteDisplay: '۲٬۵۰۰',
      currency: 'ریال',
    });
  });

  it('rounds fractional money before storage', () => {
    expect(roundMoney(100.456, 2)).toBe(100.46);
    expect(roundMoney(100.454, 2)).toBe(100.45);
  });

  it('maps currency codes to Persian labels', () => {
    expect(currencyLabel('IRR')).toBe('ریال');
    expect(currencyLabel('IRT')).toBe('تومان');
  });
});
