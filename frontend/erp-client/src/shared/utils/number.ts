/**
 * ابزارهای عددی — پشتیبانی ارقام فارسی/انگلیسی با حفظ مقدار واقعی.
 * قانون طلایی: فرمت نمایشی هرگز مقدار ذخیره‌شده را تغییر نمی‌دهد.
 */

const PERSIAN_DIGITS = '۰۱۲۳۴۵۶۷۸۹' as const;
const ARABIC_DIGITS = '٠١٢٣٤٥٦٧٨٩' as const;

/** تبدیل همه ارقام فارسی/عربی متن به انگلیسی (برای parse) */
export function digitsToEnglish(input: string): string {
  return input.replace(/[\u06F0-\u06F9\u0660-\u0669]/g, (ch) => {
    const idx = PERSIAN_DIGITS.indexOf(ch);
    return String(idx >= 0 ? idx : ARABIC_DIGITS.indexOf(ch));
  });
}

/** تبدیل ارقام انگلیسی متن به فارسی (فقط برای نمایش) */
export function digitsToPersian(input: string | number): string {
  return String(input).replace(/\d/g, (d) => PERSIAN_DIGITS[Number(d)]);
}

/** جداکننده هزارگان انگلیسی — برای مقدار واقعی و ذخیره */
export function formatNumber(value: number, fractionDigits = 0): string {
  return value.toLocaleString('en-US', {
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
  });
}

/**
 * پارس ورودی عددی کاربر: ارقام فارسی/عربی، جداکننده‌های هزارگان
 * («,» و «،»)، ممیز فارسی («/» و «٫») و فاصله را می‌پذیرد.
 * خروجی: عدد واقعی یا null اگر عدد معتبری نباشد.
 */
export function parseNumberInput(raw: string): number | null {
  if (raw == null) return null;
  const cleaned = digitsToEnglish(String(raw))
    .replace(/[,،\s\u066C]/g, '')
    .replace(/[٫\/]/g, '.')
    .replace(/[^\d.\-+]/g, '');
  if (cleaned === '' || cleaned === '-' || cleaned === '+') return null;
  const parsed = Number(cleaned);
  return Number.isFinite(parsed) ? parsed : null;
}

/** آیا متن فقط عدد (با جداکننده‌های مجاز) است؟ — strict: حروف ناشناخته ممنوع */
export function isNumericInput(raw: string): boolean {
  if (raw == null || String(raw).trim() === '') return false;
  const normalized = digitsToEnglish(String(raw)).replace(/[,،\s٫\/\u066C]/g, '');
  if (normalized === '' || normalized === '-' || normalized === '+') return false;
  return /^[+-]?\d+(\.\d+)?$/.test(normalized);
}

/** جداکننده هزارگان فارسی («٬») برای نمایش — بدون تغییر مقدار */
export function groupPersian(value: number, fractionDigits = 0): string {
  return formatNumberPersian(value, fractionDigits);
}

/** نمایش عدد با ارقام فارسی و جداکننده هزارگان فارسی («٬») */
export function formatNumberPersian(value: number, fractionDigits = 0): string {
  return digitsToPersian(formatNumber(value, fractionDigits)).replace(/,/g, '٬');
}
