import React, { Component, ErrorInfo } from 'react';
import { AlertTriangle, RotateCcw } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { toast } from '@/core/notifications/toast';

interface ErrorBoundaryProps {
  children: React.ReactNode;
}

interface ErrorBoundaryState {
  hasError: boolean;
}

/**
 * مرز خطای سطح برنامه: از propagation خطاهای رندر جلوگیری می‌کند،
 * خطا را ثبت می‌کند و یک رابط بازیابی امن ارائه می‌دهد.
 *
 * توجه: این کامپوننت فقط دامنه UI را مدیریت می‌کند؛ خطاهای درخواست‌های
 * شبکه در لایه apiClient و کوئری‌ها در QueryProvider مدیریت می‌شوند.
 */
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { hasError: false };

  static getDerivedStateFromError(): ErrorBoundaryState {
    return { hasError: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    // ثبت برای ابزار مانیتورینگ آینده (Sentry و غیره)
    console.error('[ErrorBoundary]', error, info.componentStack);
    toast.error('خطای غیرمنتظره در رابط کاربری', 'صفحه به حالت بازیابی رفت. می‌توانید تلاش مجدد کنید.');
  }

  private handleReset = (): void => {
    this.setState({ hasError: false });
    window.location.assign('/');
  };

  render(): React.ReactNode {
    if (this.state.hasError) {
      return (
        <div
          dir="rtl"
          role="alert"
          className="flex min-h-screen flex-col items-center justify-center gap-4 bg-background p-6 text-center"
        >
          <AlertTriangle className="size-12 text-destructive" aria-hidden />
          <h1 className="text-xl font-bold text-foreground">خطای غیرمنتظره رخ داد</h1>
          <p className="max-w-md text-sm text-muted-foreground">
            متأسفیم؛ یک خطای داخلی مانع نمایش این بخش شد. با تلاش مجدد یا بازگشت به صفحه اصلی می‌توانید ادامه دهید.
          </p>
          <Button onClick={this.handleReset} variant="outline">
            <RotateCcw className="size-4" aria-hidden />
            بازگشت به صفحه اصلی
          </Button>
        </div>
      );
    }

    return this.props.children;
  }
}
