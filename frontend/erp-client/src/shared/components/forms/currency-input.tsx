import * as React from 'react';
import { Input } from '@/shared/components/ui/input';
import { cn } from '@/shared/utils/cn';
import { parseMoneyInput, roundMoney } from '@/shared/utils/money';

export interface CurrencyInputProps
  extends Omit<React.ComponentProps<'input'>, 'value' | 'onChange' | 'type'> {
  /** مبلغ واقعی — همیشه number؛ نمایش فرمت‌شده مستقل از مقدار است */
  value: number | null;
  onChange: (value: number | null) => void;
  /** واحد پول نمایشی سمت چپ فیلد */
  currencyLabel?: string;
  /** حداکثر رقم اعشار هنگام commit (پیش‌فرض ۰) */
  fractionDigits?: number;
  /** اجازه مقادیر منفی (سند تعدیلی و…) */
  allowNegative?: boolean;
}

/**
 * ورودی مبلغ: grouping زنده هنگام تایپ، پارس ارقام فارسی/انگلیسی،
 * و commit مقدار واقعی. نمایش هرگز مقدار ذخیره‌شده را تغییر نمی‌دهد.
 */
export const CurrencyInput = React.forwardRef<HTMLInputElement, CurrencyInputProps>(
  ({ value, onChange, currencyLabel = 'ریال', fractionDigits = 0, allowNegative = false, className, onBlur, disabled, ...props }, ref) => {
    const [displayText, setDisplayText] = React.useState<string>(() => (value === null ? '' : formatGrouped(value, fractionDigits)));

    React.useEffect(() => {
      setDisplayText(value === null ? '' : formatGrouped(value, fractionDigits));
    }, [value, fractionDigits]);

    const handleBlur: React.FocusEventHandler<HTMLInputElement> = (event) => {
      const parsed = event.target.value.trim() === '' ? null : parseMoneyInput(event.target.value);
      if (parsed === null) {
        onChange(null);
        setDisplayText('');
      } else {
        const safe = allowNegative ? parsed : Math.abs(parsed);
        const rounded = roundMoney(safe, fractionDigits);
        onChange(rounded);
        setDisplayText(formatGrouped(rounded, fractionDigits));
      }
      onBlur?.(event);
    };

    return (
      <div className="relative">
        <Input
          ref={ref}
          type="text"
          inputMode="decimal"
          dir="ltr"
          className={cn('num-en pl-14 text-start', className)}
          disabled={disabled}
          value={displayText}
          onChange={(event) => {
            // حین تایپ فقط متن به‌روز می‌شود؛ commit واقعی در blur
            setDisplayText(event.target.value);
            const parsed = event.target.value.trim() === '' ? null : parseMoneyInput(event.target.value);
            if (parsed !== null) onChange(allowNegative ? parsed : Math.abs(parsed));
            else onChange(null);
          }}
          onBlur={handleBlur}
          {...props}
        />
        <span className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-xs text-muted-foreground">
          {currencyLabel}
        </span>
      </div>
    );
  },
);
CurrencyInput.displayName = 'CurrencyInput';

function formatGrouped(amount: number, fractionDigits: number): string {
  return amount.toLocaleString('en-US', {
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
  });
}
