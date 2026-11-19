import React from 'react';
import { ResponsiveContainer } from 'recharts';
import { cn } from '@/shared/utils/cn';

interface ChartContainerProps {
  /** ارتفاع پیش‌فرض نمودار به پیکسل؛ قابل override با کلاس ارتفاع */
  height?: number;
  className?: string;
  'aria-label'?: string;
  children: React.ReactElement;
}

/**
 * ظرف استاندارد نمودارها (Recharts): RTL، پس‌زمینه کارت، جهت و فونت موضوعی.
 * کامپوننت‌های نمودار (Bar/Line/Pie) به‌عنوان فرزند داخل آن رندر می‌شوند و
 * تنظیمات خوانایی مشترک (فونت، رنگ‌ها با متغیرهای CSS) در globals.css متمرکز است.
 *
 * مثال:
 *   <ChartContainer aria-label="فروش ماهانه">
 *     <BarChart data={data}> ... </BarChart>
 *   </ChartContainer>
 */
export const ChartContainer: React.FC<ChartContainerProps> = ({
  height = 320,
  className,
  'aria-label': ariaLabel = 'نمودار',
  children,
}) => {
  return (
    <div
      dir="rtl"
      role="img"
      aria-label={ariaLabel}
      style={{ height }}
      className={cn('w-full rounded-lg border bg-card p-4', className)}
    >
      <ResponsiveContainer width="100%" height="100%">
        {children}
      </ResponsiveContainer>
    </div>
  );
};
