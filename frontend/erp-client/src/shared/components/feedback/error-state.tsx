import * as React from 'react';
import { AlertTriangle, RotateCcw } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { cn } from '@/shared/utils/cn';

export interface ErrorStateProps {
  title?: string;
  description?: string;
  onRetry?: () => void;
  className?: string;
}

/** حالت خطای واکشی داده — جای خالی‌ماندن جدول/پنل می‌ایستد */
export const ErrorState: React.FC<ErrorStateProps> = ({
  title = 'خطا در بارگذاری داده‌ها',
  description = 'در دریافت اطلاعات مشکلی پیش آمد. لطفا دوباره تلاش کنید.',
  onRetry,
  className,
}) => (
  <div
    role="alert"
    className={cn(
      'flex flex-col items-center justify-center gap-3 rounded-lg border border-danger/30 bg-danger/5 p-8 text-center',
      className,
    )}
  >
    <AlertTriangle className="size-8 text-danger" aria-hidden />
    <p className="text-sm font-bold text-foreground">{title}</p>
    <p className="max-w-sm text-xs leading-5 text-muted-foreground">{description}</p>
    {onRetry && (
      <Button variant="outline" size="sm" onClick={onRetry}>
        <RotateCcw className="size-3.5" aria-hidden />
        تلاش مجدد
      </Button>
    )}
  </div>
);
