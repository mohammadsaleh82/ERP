import * as React from 'react';
import { ChevronRight, ChevronLeft } from 'lucide-react';
import { cn } from '@/shared/utils/cn';
import { IconButton } from '@/shared/components/ui/icon-button';
import { digitsToPersian } from '@/shared/utils/number';

export interface PaginationProps {
  page: number;
  pageCount: number;
  onChange: (page: number) => void;
  className?: string;
}

/**
 * صفحه‌بندی RTL با ارقام فارسی و ناوبری کیبورد (دکمه‌های ناتیو).
 * دکمه‌های قبلی/بعدی بر اساس جهت RTL معکوس‌اند: «قبلی» به راست می‌رود.
 */
export const Pagination: React.FC<PaginationProps> = ({ page, pageCount, onChange, className }) => {
  const canPrev = page > 1;
  const canNext = page < pageCount;

  const windowStart = Math.max(1, Math.min(page - 2, Math.max(1, pageCount - 4)));
  const windowEnd = Math.min(pageCount, windowStart + 4);
  const pages = Array.from({ length: windowEnd - windowStart + 1 }, (_, i) => windowStart + i);

  return (
    <nav aria-label="صفحه‌بندی" className={cn('flex items-center justify-center gap-1', className)}>
      <IconButton aria-label="صفحه قبل" variant="outline" size="sm" disabled={!canPrev} onClick={() => onChange(page - 1)}>
        <ChevronRight className="size-4" aria-hidden />
      </IconButton>
      {pages.map((p) => (
        <button
          key={p}
          type="button"
          aria-current={p === page ? 'page' : undefined}
          onClick={() => onChange(p)}
          className={cn(
            'size-8 cursor-pointer rounded-md text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
            p === page ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:bg-muted',
          )}
        >
          {digitsToPersian(p)}
        </button>
      ))}
      <IconButton aria-label="صفحه بعد" variant="outline" size="sm" disabled={!canNext} onClick={() => onChange(page + 1)}>
        <ChevronLeft className="size-4" aria-hidden />
      </IconButton>
    </nav>
  );
};
