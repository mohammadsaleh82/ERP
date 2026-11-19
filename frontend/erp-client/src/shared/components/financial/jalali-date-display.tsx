import * as React from 'react';
import { formatJalaliLong, formatDualDate, formatToPersianDate } from '@/shared/utils/date';
import { cn } from '@/shared/utils/cn';

export interface JalaliDateDisplayProps {
  date: string | Date | null | undefined;
  /** قالب نمایش: کوتاه (۱۴۰۳/۱۰/۱۵)، کامل با نام ماه، یا دوگانه با میلادی */
  format?: 'short' | 'long' | 'dual';
  className?: string;
  'aria-label'?: string;
}

/** نمایش تاریخ شمسی با قالب‌های مشخص — مقدار اصلی (ISO) دست‌نخورده می‌ماند */
export const JalaliDateDisplay: React.FC<JalaliDateDisplayProps> = ({ date, format = 'short', className, 'aria-label': ariaLabel }) => {
  const label =
    format === 'long' ? formatJalaliLong(date) : format === 'dual' ? formatDualDate(date) : formatToPersianDate(date);
  return (
    <time
      className={cn('inline-block text-start', className)}
      aria-label={ariaLabel}
      title={date ? formatGregorianSafe(date) : undefined}
    >
      {label}
    </time>
  );
};

function formatGregorianSafe(date: string | Date): string | undefined {
  const d = typeof date === 'string' ? new Date(date) : date;
  return isNaN(d.getTime()) ? undefined : d.toISOString();
}
