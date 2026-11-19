import * as React from 'react';
import { ChevronDown } from 'lucide-react';
import { cn } from '@/shared/utils/cn';

export interface SelectOption {
  value: string;
  label: string;
  disabled?: boolean;
}

export interface SelectProps extends React.SelectHTMLAttributes<HTMLSelectElement> {
  options: SelectOption[];
  /** متن گزینه خالی — وقتی مقدار '' است */
  placeholder?: string;
}

/** Select استاندارد (native) — دسترس‌پذیری کامل کیبورد، استایل یکسان با Input */
export const Select = React.forwardRef<HTMLSelectElement, SelectProps>(
  ({ options, placeholder, className, children, ...props }, ref) => (
    <div className="relative">
      <select
        ref={ref}
        className={cn(
          'flex h-9 w-full appearance-none cursor-pointer items-center rounded-md border border-input bg-surface px-3 text-sm shadow-xs transition-colors',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
          'disabled:cursor-not-allowed disabled:opacity-50 aria-[invalid=true]:border-danger',
          className,
        )}
        {...props}
      >
        {placeholder !== undefined && <option value="">{placeholder}</option>}
        {children ?? options.map((opt) => (
          <option key={opt.value} value={opt.value} disabled={opt.disabled}>
            {opt.label}
          </option>
        ))}
      </select>
      <ChevronDown className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden />
    </div>
  ),
);
Select.displayName = 'Select';
