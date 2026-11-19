/**
 * ابزارهای مبلغ — نمایش/پارس مبلغ با واحد پول.
 * قانون طلایی: فرمت نمایشی هرگز مقدار ذخیره‌شده را تغییر نمی‌دهد؛
 * مقدار واقعی همیشه number انگلیسی است.
 */
import {
  formatNumber,
  formatNumberPersian,
  parseNumberInput,
} from '@/shared/utils/number';

export type CurrencyCode = 'IRR' | 'IRT' | 'USD' | 'EUR';

const CURRENCY_LABEL: Record<CurrencyCode, string> = {
  IRR: 'ریال',
  IRT: 'تومان',
  USD: 'دلار',
  EUR: 'یورو',
};

export interface MoneyFormatOptions {
  /** نمایش واحد پول کنار مبلغ */
  withCurrency?: boolean;
  /** ارقام فارسی (پیش‌فرض) یا انگلیسی */
  persianDigits?: boolean;
  /** تعداد رقم اعشار (پیش‌فرض صفر — ریال/تومان) */
  fractionDigits?: number;
  /** نمایش صفر به‌صورت «—» (مثل مانده صفر) */
  zeroAsDash?: boolean;
}

/** برچسب فارسی واحد پول */
export function currencyLabel(currency: CurrencyCode): string {
  return CURRENCY_LABEL[currency];
}

/** گرد کردن به تعداد اعشار مجاز — قبل از ذخیره */
export function roundMoney(amount: number, fractionDigits = 0): number {
  const factor = 10 ** fractionDigits;
  return Math.round((amount + Number.EPSILON) * factor) / factor;
}

/**
 * نمایش مبلغ: جداکننده هزارگان + علامت منفی استاندارد + واحد پول.
 * مثال: 1250000 → «۱٬۲۵۰٬۰۰۰ ریال»
 */
export function formatMoney(amount: number, options: MoneyFormatOptions = {}): string {
  const {
    withCurrency = false,
    persianDigits = true,
    fractionDigits = 0,
    zeroAsDash = false,
  } = options;

  if (zeroAsDash && amount === 0) return '—';

  const rounded = roundMoney(amount, fractionDigits);
  const grouped = formatNumber(Math.abs(rounded), fractionDigits);
  const sign = rounded < 0 ? '−' : '';
  const body = persianDigits ? formatNumberPersian(Math.abs(rounded), fractionDigits) : grouped;
  const currency = withCurrency ? ` ${CURRENCY_LABEL.IRR}` : '';

  return `${sign}${body}${currency}`;
}

/**
 * پارس مبلغ ورودی کاربر به مقدار واقعی (number).
 * ارقام فارسی/انگلیسی و جداکننده‌ها را می‌پذیرد؛ نامعتبر → null.
 */
export function parseMoneyInput(raw: string): number | null {
  return parseNumberInput(raw);
}

/** آیا مقدار منفی است؟ — برای رنگ بدهکار/بستانکار */
export function isNegativeMoney(amount: number): boolean {
  return amount < 0;
}

/** جداسازی بخش‌های نمایشی مبلغ برای رندر رنگی در UI */
export interface MoneyParts {
  sign: '−' | '';
  absoluteDisplay: string;
  currency: string;
}

export function moneyParts(amount: number, currency: CurrencyCode = 'IRR', persianDigits = true): MoneyParts {
  return {
    sign: amount < 0 ? '−' : '',
    absoluteDisplay: persianDigits
      ? formatNumberPersian(Math.abs(amount))
      : formatNumber(Math.abs(amount)),
    currency: CURRENCY_LABEL[currency],
  };
}
