import * as React from 'react';
import { Link } from 'react-router-dom';
import { ChevronLeft } from 'lucide-react';
import { cn } from '@/shared/utils/cn';

export interface BreadcrumbItem {
  label: string;
  href?: string;
}

export interface BreadcrumbProps {
  items: BreadcrumbItem[];
  className?: string;
}

/** نان‌خردکن RTL: فاصله‌گذاری یکنواخت، آخرین لنگر بدون لینک */
export const Breadcrumb: React.FC<BreadcrumbProps> = ({ items, className }) => (
  <nav aria-label="مسیر جاری" className={cn('flex min-w-0 items-center gap-1.5 text-sm', className)}>
    <ol className="flex min-w-0 items-center gap-1.5">
      {items.map((item, index) => {
        const isLast = index === items.length - 1;
        return (
          <li key={`${item.label}-${index}`} className="flex min-w-0 items-center gap-1.5">
            {index > 0 && <ChevronLeft className="size-3.5 shrink-0 text-muted-foreground" aria-hidden />}
            {item.href && !isLast ? (
              <Link to={item.href} className="truncate text-muted-foreground transition-colors hover:text-foreground">
                {item.label}
              </Link>
            ) : (
              <span aria-current={isLast ? 'page' : undefined} className={cn('truncate', isLast ? 'font-semibold text-foreground' : 'text-muted-foreground')}>
                {item.label}
              </span>
            )}
          </li>
        );
      })}
    </ol>
  </nav>
);
