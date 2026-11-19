import * as React from 'react';
import { cn } from '@/shared/utils/cn';

interface SkeletonProps extends React.HTMLAttributes<HTMLDivElement> {
  /** شکل مستطیلی (پیش‌فرض) یا دایره (آواتار) */
  shape?: 'rect' | 'circle';
}

/** Placeholder بارگذاری — به‌جای Spinner برای لیست‌های مالی */
export const Skeleton: React.FC<SkeletonProps> = ({ className, shape = 'rect', ...props }) => (
  <div
    aria-hidden
    className={cn(
      'animate-pulse bg-muted',
      shape === 'circle' ? 'rounded-full' : 'rounded-md',
      className,
    )}
    {...props}
  />
);
