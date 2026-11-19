import * as React from 'react';
import { Badge } from '@/shared/components/ui/badge';
import { DateRangePickerJalali } from '@/shared/components/forms/date-range-picker-jalali';
import { fiscalPeriodStatusLabel, type FiscalPeriodStatus } from '@/shared/utils/status';

/* ── FiscalPeriodStatusBadge ──────────────────────────────────────── */
const toneToVariant = {
  neutral: 'secondary',
  warning: 'warning',
  success: 'success',
  info: 'info',
  primary: 'default',
} as const;

export interface FiscalPeriodStatusBadgeProps {
  status: string;
  className?: string;
}

/** Badge وضعیت دوره مالی (باز/بسته/تعدیل) */
export const FiscalPeriodStatusBadge: React.FC<FiscalPeriodStatusBadgeProps> = ({ status, className }) => {
  const descriptor = fiscalPeriodStatusLabel(status);
  return <Badge variant={toneToVariant[descriptor.tone]} className={className}>{descriptor.label}</Badge>;
};

/* ── FiscalPeriodPicker ───────────────────────────────────────────── */
export interface FiscalPeriod {
  id: string;
  title: string;
  status: string;
}

export interface FiscalPeriodPickerProps {
  /** دوره‌های مالی فعال سازمان */
  periods: readonly FiscalPeriod[];
  value: string | null;
  onChange: (periodId: string) => void;
  /** بازه سفارشی — وقتی «دوره دیگر» انتخاب شود */
  customFrom?: string | null;
  customTo?: string | null;
  onCustomRangeChange?: (range: { from: string | null; to: string | null }) => void;
  disabled?: boolean;
}

/** انتخاب بازه مالی: از دوره‌های تعریف‌شده یا بازه شمسی سفارشی */
export const FiscalPeriodPicker: React.FC<FiscalPeriodPickerProps> = ({
  periods,
  value,
  onChange,
  customFrom,
  customTo,
  onCustomRangeChange,
  disabled,
}) => {
  const isCustom = value === 'custom';
  return (
    <div className="space-y-3">
      <div className="flex flex-wrap gap-2" role="radiogroup" aria-label="بازه مالی">
        {periods.map((period) => (
          <button
            key={period.id}
            type="button"
            role="radio"
            aria-checked={period.id === value}
            disabled={disabled}
            onClick={() => onChange(period.id)}
            className={
              period.id === value
                ? 'inline-flex cursor-pointer items-center gap-2 rounded-full border border-primary bg-primary-soft px-3.5 py-1.5 text-sm font-medium text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
                : 'inline-flex cursor-pointer items-center gap-2 rounded-full border border-input bg-surface px-3.5 py-1.5 text-sm text-muted-foreground transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50'
            }
          >
            {period.title}
            <FiscalPeriodStatusBadge status={period.status} />
          </button>
        ))}
        <button
          type="button"
          role="radio"
          aria-checked={isCustom}
          disabled={disabled || !onCustomRangeChange}
          onClick={() => onCustomRangeChange && onChange('custom')}
          className={
            isCustom
              ? 'inline-flex cursor-pointer items-center rounded-full border border-primary bg-primary-soft px-3.5 py-1.5 text-sm font-medium text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
              : 'inline-flex cursor-pointer items-center rounded-full border border-input bg-surface px-3.5 py-1.5 text-sm text-muted-foreground transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50'
          }
        >
          بازه سفارشی
        </button>
      </div>
      {isCustom && onCustomRangeChange && (
        <DateRangePickerJalali
          from={customFrom ?? null}
          to={customTo ?? null}
          onChange={onCustomRangeChange}
          disabled={disabled}
        />
      )}
    </div>
  );
};

export type { FiscalPeriodStatus };
