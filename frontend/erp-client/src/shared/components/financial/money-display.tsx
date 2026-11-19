import * as React from 'react';
import { cn } from '@/shared/utils/cn';
import { formatMoney, type MoneyFormatOptions } from '@/shared/utils/money';

/* ── MoneyDisplay ─────────────────────────────────────────────────── */
export interface MoneyDisplayProps {
  amount: number;
  options?: MoneyFormatOptions;
  /** رنگ‌بندی منفی‌ها با قرمز */
  highlightNegative?: boolean;
  className?: string;
  'aria-label'?: string;
}

/** نمایش مبلغ با جداکننده هزارگان، ارقام فارسی و اختیاراً واحد پول */
export const MoneyDisplay: React.FC<MoneyDisplayProps> = ({
  amount,
  options,
  highlightNegative = true,
  className,
  'aria-label': ariaLabel,
}) => (
  <span
    dir="ltr"
    className={cn(
      'num-en inline-block text-start font-medium',
      highlightNegative && amount < 0 && 'text-danger',
      className,
    )}
    aria-label={ariaLabel}
  >
    {formatMoney(amount, options)}
  </span>
);

/* ── DebitCreditDisplay ───────────────────────────────────────────── */
export interface DebitCreditDisplayProps {
  /** مبلغ مثبت با ماهیت: 'debit' بدهکار | 'credit' بستانکار */
  amount: number;
  nature: 'debit' | 'credit';
  className?: string;
}

/** نمایش ستون بدهکار/بستانکار: مبلغ با پس‌زمینه رنگی ملایم ماهیت */
export const DebitCreditDisplay: React.FC<DebitCreditDisplayProps> = ({ amount, nature, className }) => {
  if (amount === 0) {
    return (
      <span dir="ltr" className={cn('num-en inline-block text-start text-muted-foreground', className)}>
        {formatMoney(0)}
      </span>
    );
  }
  return (
    <span
      dir="ltr"
      className={cn(
        'num-en inline-block rounded px-1.5 py-0.5 text-start font-medium',
        nature === 'debit' ? 'bg-debit-soft text-debit' : 'bg-credit-soft text-credit',
        className,
      )}
    >
      {formatMoney(amount)}
    </span>
  );
};

/* ── BalanceDisplay ───────────────────────────────────────────────── */
export interface BalanceDisplayProps {
  /** مانده مثبت = بدهکار، منفی = بستانکار، صفر = تسویه */
  balance: number;
  currency?: string;
  className?: string;
}

/** نمایش مانده حساب با ماهیت واضح — اصل خوانایی داده مالی */
export const BalanceDisplay: React.FC<BalanceDisplayProps> = ({ balance, currency, className }) => {
  if (balance === 0) {
    return (
      <span className={cn('inline-flex items-center rounded bg-muted px-2 py-0.5 text-sm text-neutral-balance', className)}>
        مانده صفر
      </span>
    );
  }
  const isDebit = balance > 0;
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded px-2 py-0.5 text-sm font-medium',
        isDebit ? 'bg-debit-soft text-debit' : 'bg-credit-soft text-credit',
        className,
      )}
    >
      <span dir="ltr" className="num-en">
        {formatMoney(Math.abs(balance))}
      </span>
      <span>{isDebit ? 'بدهکار' : 'بستانکار'}</span>
      {currency && <span className="text-xs opacity-75">({currency})</span>}
    </span>
  );
};
