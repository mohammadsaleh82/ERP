import * as React from 'react';
import { Filter, RotateCcw } from 'lucide-react';
import { Input } from '@/shared/components/ui/input';
import { Button } from '@/shared/components/ui/button';
import { Tooltip } from '@/shared/components/ui/tooltip';

export interface DateRangeFilterProps {
  from: string | undefined;
  to: string | undefined;
  onChange: (range: { from?: string; to?: string }) => void;
}

/**
 * فیلتر بازه زمانی مشترک داشبورد — وضعیت در URL نگه داشته می‌شود
 * (state قابل بازگشت + shareable). تاریخ‌ها ISO yyyy-MM-dd.
 */
export const DateRangeFilter: React.FC<DateRangeFilterProps> = ({ from, to, onChange }) => {
  return (
    <div className="flex flex-wrap items-center gap-2 rounded-lg border border-border/60 bg-surface px-3 py-2" data-testid="date-range-filter">
      <span className="flex items-center gap-1.5 text-xs font-semibold text-foreground">
        <Filter className="size-3.5 text-primary" aria-hidden />
        بازه زمانی
      </span>
      <label className="sr-only" htmlFor="dash-from">از تاریخ</label>
      <Input
        id="dash-from"
        type="date"
        dir="ltr"
        className="h-8 w-40 text-xs"
        value={from ?? ''}
        max={to}
        onChange={(event) => onChange({ from: event.target.value || undefined, to })}
      />
      <span className="text-xs text-muted-foreground">تا</span>
      <label className="sr-only" htmlFor="dash-to">تا تاریخ</label>
      <Input
        id="dash-to"
        type="date"
        dir="ltr"
        className="h-8 w-40 text-xs"
        value={to ?? ''}
        min={from}
        onChange={(event) => onChange({ from, to: event.target.value || undefined })}
      />
      {(from || to) && (
        <Tooltip content="حذف فیلتر بازه">
          <Button variant="ghost" size="icon" className="size-7" aria-label="حذف فیلتر بازه" onClick={() => onChange({ from: undefined, to: undefined })}>
            <RotateCcw className="size-3.5" aria-hidden />
          </Button>
        </Tooltip>
      )}
    </div>
  );
};
