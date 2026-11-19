import * as React from 'react';
import { Input } from '@/shared/components/ui/input';
import { cn } from '@/shared/utils/cn';
import { parseNumberInput, digitsToPersian, formatNumber } from '@/shared/utils/number';

export interface NumberInputProps
  extends Omit<React.ComponentProps<'input'>, 'value' | 'onChange' | 'type'> {
  /** مقدار واقعی (number) — display formatting رابط کاربری است و مقدار را تغییر نمی‌دهد */
  value: number | null;
  onChange: (value: number | null) => void;
  /** جداکننده هزارگان هنگام تایپ */
  grouping?: boolean;
  min?: number;
  max?: number;
  step?: number;
}

/**
 * ورودی عددی: کاربر با ارقام فارسی/انگلیسی و هر جداکننده‌ای تایپ می‌کند؛
 * state فرم همیشه مقدار واقعی (number|null) است، نه متن فرمت‌شده.
 */
export const NumberInput = React.forwardRef<HTMLInputElement, NumberInputProps>(
  ({ value, onChange, grouping = true, min, max, step, className, onBlur, disabled, ...props }, ref) => {
    const [displayText, setDisplayText] = React.useState<string>(() => (value === null ? '' : formatNumber(value)));

    React.useEffect(() => {
      // همگام‌سازی از بیرون: فقط وقتی تفاوت واقعی وجود دارد
      setDisplayText(value === null ? '' : formatNumber(value));
    }, [value]);

    const commit = (raw: string) => {
      const parsed = raw.trim() === '' ? null : parseNumberInput(raw);
      onChange(parsed);
    };

    const clamp = (num: number): number => {
      if (typeof min === 'number' && num < min) return min;
      if (typeof max === 'number' && num > max) return max;
      return num;
    };

    const handleBlur: React.FocusEventHandler<HTMLInputElement> = (event) => {
      const parsed = event.target.value.trim() === '' ? null : parseNumberInput(event.target.value);
      if (parsed !== null) {
        const clamped = clamp(parsed);
        onChange(clamped);
        setDisplayText(grouping ? formatNumber(clamped) : String(clamped));
      } else {
        setDisplayText('');
      }
      onBlur?.(event);
    };

    return (
      <Input
        ref={ref}
        type="text"
        inputMode="decimal"
        dir="ltr"
        className={cn('num-en text-start', className)}
        disabled={disabled}
        value={displayText}
        onChange={(event) => {
          setDisplayText(event.target.value);
          commit(event.target.value);
        }}
        onBlur={handleBlur}
        aria-valuenow={value ?? undefined}
        {...props}
      />
    );
  },
);
NumberInput.displayName = 'NumberInput';

/** helper نمایش مقدار فارسی برای placeholder/hint های کنار NumberInput */
export const persianNumberHint = (value: number): string => digitsToPersian(formatNumber(value));
