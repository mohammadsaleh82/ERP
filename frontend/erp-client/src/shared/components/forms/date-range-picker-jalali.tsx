import * as React from 'react';
import { CalendarRange } from 'lucide-react';
import { DatePickerJalali } from '@/shared/components/forms/date-picker-jalali';
import { formatJalaliRange } from '@/shared/utils/date';
import { cn } from '@/shared/utils/cn';

export interface DateRangePickerJalaliProps {
  from: string | null;
  to: string | null;
  /** خطای بازه — مثل «تا پیش از از است» */
  error?: string;
  onChange: (range: { from: string | null; to: string | null }) => void;
  disabled?: boolean;
  className?: string;
}

/** انتخاب بازه شمسی: دو DatePicker + خلاصه بازه + اعتبارسنجی ترتیب */
export const DateRangePickerJalali: React.FC<DateRangePickerJalaliProps> = ({
  from,
  to,
  error,
  onChange,
  disabled,
  className,
}) => {
  const rangeError =
    error ??
    (from && to && new Date(from).getTime() > new Date(to).getTime() ? 'تاریخ «تا» باید بعد از تاریخ «از» باشد' : undefined);

  return (
    <div className={cn('space-y-2', className)}>
      <div className="flex items-center gap-2">
        <DatePickerJalali
          value={from ?? undefined}
          disabled={disabled}
          onChange={(iso) => onChange({ from: iso, to })}
          aria-label="از تاریخ"
        />
        <span className="shrink-0 text-xs text-muted-foreground">تا</span>
        <DatePickerJalali
          value={to ?? undefined}
          disabled={disabled}
          onChange={(iso) => onChange({ from, to: iso })}
          aria-label="تا تاریخ"
        />
      </div>
      {!rangeError && (from || to) && (
        <p className="flex items-center gap-1.5 text-xs text-muted-foreground">
          <CalendarRange className="size-3.5" aria-hidden />
          {formatJalaliRange(from, to)}
        </p>
      )}
      {rangeError && (
        <p role="alert" className="text-xs font-medium text-danger">
          {rangeError}
        </p>
      )}
    </div>
  );
};
