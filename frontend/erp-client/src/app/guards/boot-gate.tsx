import * as React from 'react';
import { restoreSession } from '@/core/auth/session';

/**
 * Loading اولیه: بازیابی silent نشست از Cookie refresh قبل از رندر routes.
 * از فلش صفحه login در reload جلوگیری می‌کند (مدیریت انقضای نشست نرم).
 */
export const BootGate: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [booted, setBooted] = React.useState(false);

  React.useEffect(() => {
    let cancelled = false;
    void restoreSession().finally(() => {
      if (!cancelled) setBooted(true);
    });
    return () => {
      cancelled = true;
    };
  }, []);

  if (!booted) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-background" role="status" aria-live="polite">
        <div className="flex size-12 items-center justify-center rounded-xl bg-primary text-primary-foreground font-black text-xl">
          E
        </div>
        <p className="text-sm text-muted-foreground">در حال بازیابی نشست کاری…</p>
        <div className="h-1 w-40 overflow-hidden rounded-full bg-muted">
          <div className="h-full w-1/3 animate-pulse rounded-full bg-primary" />
        </div>
      </div>
    );
  }

  return <>{children}</>;
};
