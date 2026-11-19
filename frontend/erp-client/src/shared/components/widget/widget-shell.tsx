import * as React from 'react';
import { ShieldOff, AlertTriangle, Inbox, RefreshCw } from 'lucide-react';
import { Card, CardContent, CardHeader } from '@/shared/components/ui/card';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { Button } from '@/shared/components/ui/button';
import { cn } from '@/shared/utils/cn';

export interface WidgetShellProps {
  title: string;
  /** اختیاری: بخش عنوان سمت چپ (badge/لینک) */
  action?: React.ReactNode;
  isLoading?: boolean;
  isError?: boolean;
  isEmpty?: boolean;
  isUnauthorized?: boolean;
  /** پیام حالت empty */
  emptyMessage?: string;
  /** پیام نیاز Backend — Empty با برچسب شفاف */
  requiresBackend?: boolean;
  onRetry?: () => void;
  errorDetail?: string;
  className?: string;
  /** بدون padding (برای chartها) */
  bodyClassName?: string;
  children: React.ReactNode;
}

/**
 * پوسته مستقل هر Widget: خطای یک widget هیچ‌وقت کل داشبورد را نمی‌اندازد.
 * حالت‌ها: Loading → Success / Empty / Error / Unauthorized.
 */
export const WidgetShell: React.FC<WidgetShellProps> = ({
  title,
  action,
  isLoading,
  isError,
  isEmpty,
  isUnauthorized,
  emptyMessage = 'داده‌ای برای نمایش وجود ندارد',
  requiresBackend,
  onRetry,
  errorDetail,
  className,
  bodyClassName,
  children,
}) => {
  return (
    <Card className={cn('flex min-w-0 flex-col', className)}>
      <CardHeader className="flex flex-row items-center justify-between gap-2 border-b border-border/60 pb-3">
        <h3 className="text-sm font-bold text-foreground">{title}</h3>
        {action}
      </CardHeader>
      <CardContent className={cn('flex-1 p-4', bodyClassName)}>
        {isLoading ? (
          <div className="space-y-2.5" aria-busy="true" role="status">
            <Skeleton className="h-4 w-2/3" />
            <Skeleton className="h-4 w-1/2" />
            <Skeleton className="h-16 w-full" />
            <Skeleton className="h-4 w-1/3" />
          </div>
        ) : isUnauthorized ? (
          <div className="flex flex-col items-center gap-2 py-6 text-center">
            <ShieldOff className="size-6 text-muted-foreground" aria-hidden />
            <p className="text-xs font-medium text-muted-foreground">مجوز مشاهده این بخش را ندارید</p>
          </div>
        ) : isError ? (
          <div className="flex flex-col items-center gap-2 py-6 text-center">
            <AlertTriangle className="size-6 text-warning" aria-hidden />
            <p className="text-xs font-semibold text-foreground">نمایش این بخش ناموفق بود</p>
            {errorDetail && <p className="max-w-xs truncate text-[11px] text-muted-foreground">{errorDetail}</p>}
            {onRetry && (
              <Button variant="outline" size="sm" className="mt-1" onClick={onRetry}>
                <RefreshCw className="size-3.5" aria-hidden />
                تلاش مجدد
              </Button>
            )}
          </div>
        ) : isEmpty ? (
          <div className="flex flex-col items-center gap-2 py-6 text-center">
            <Inbox className="size-6 text-muted-foreground" aria-hidden />
            <p className="text-xs font-medium text-muted-foreground">{emptyMessage}</p>
            {requiresBackend && (
              <span className="rounded border bg-muted/40 px-2 py-0.5 text-[10px] text-muted-foreground">
                نیاز Backend: API مربوطه موجود نیست
              </span>
            )}
          </div>
        ) : (
          children
        )}
      </CardContent>
    </Card>
  );
};
