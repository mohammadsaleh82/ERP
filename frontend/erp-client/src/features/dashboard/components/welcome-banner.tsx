import * as React from 'react';
import { useCompanyStore } from '@/core/company/company.store';
import { useAuthStore } from '@/core/auth/auth.store';
import { formatToPersianDate } from '@/shared/utils/date';
import { digitsToPersian } from '@/shared/utils/number';

/** خوشامدگویی شخصی‌سازی‌شده + زمینه کاری فعال (شرکت واقعی و سال مالی) */
export const WelcomeBanner: React.FC = () => {
  const user = useAuthStore((state) => state.user);
  const { activeCompany, activeTenant } = useCompanyStore();

  const greeting = React.useMemo(() => {
    const hour = new Date().getHours();
    if (hour < 12) return 'صبح‌بخیر';
    if (hour < 17) return 'بعدازظهر بخیر';
    return 'شب‌بخیر';
  }, []);

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-border bg-gradient-to-l from-primary-soft/70 to-surface px-4 py-3.5" data-testid="welcome-banner">
      <div className="min-w-0">
        <h2 className="text-base font-bold text-foreground">
          {greeting}، {user?.fullName || user?.email || 'کاربر'} 👋
        </h2>
        <p className="mt-0.5 truncate text-xs text-muted-foreground">
          {activeCompany
            ? `زمینه کاری فعال: ${activeCompany.name}${activeTenant ? ` — ${activeTenant.name}` : ''}`
            : 'شرکت فعالی انتخاب نشده است'}
        </p>
      </div>
      <div className="text-xs text-muted-foreground">
        امروز {formatToPersianDate(new Date())}
        {activeCompany && ` · سال مالی ${digitsToPersian(activeCompany.fiscalYear)}`}
      </div>
    </div>
  );
};
