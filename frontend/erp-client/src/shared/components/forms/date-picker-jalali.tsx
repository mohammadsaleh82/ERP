import React from 'react';
import { Calendar as CalendarIcon } from 'lucide-react';
import { Input } from '@/shared/components/ui/input';
import { formatToPersianDate } from '@/shared/utils/date';
import { cn } from '@/shared/utils/cn';

interface DatePickerJalaliProps {
  value?: string; // ISO UTC string
  onChange?: (isoUtcDate: string) => void;
  label?: string;
  error?: string;
  disabled?: boolean;
  className?: string;
  'aria-label'?: string;
}

export const DatePickerJalali: React.FC<DatePickerJalaliProps> = ({
  value,
  onChange,
  label,
  error,
  disabled = false,
  className,
  'aria-label': ariaLabel,
}) => {
  const displayValue = value ? formatToPersianDate(value) : '';

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    // When integrated with custom picker, this emits the ISO UTC string
    onChange?.(e.target.value);
  };

  return (
    <div className={cn('space-y-1', className)}>
      {label && <label className="text-sm font-medium text-foreground">{label}</label>}
      <div className="relative">
        <Input
        aria-label={ariaLabel}
          type="text"
          readOnly
          value={displayValue}
          placeholder="۱۴۰۳/۰۸/۲۵"
          error={error}
          disabled={disabled}
          onChange={handleInputChange}
          className="ps-10 font-tabular cursor-pointer"
        />
        <CalendarIcon className="absolute start-3 top-2.5 h-4 w-4 text-muted-foreground pointer-events-none" />
      </div>
    </div>
  );
};
