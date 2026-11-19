import * as React from 'react';
import { WidgetShell } from '@/shared/components/widget/widget-shell';
import { useDashboardData } from '@/features/dashboard/dashboard.queries';
import { formatToPersianDateTime } from '@/shared/utils/date';

/** فعالیت‌های اخیر — دفتر ممیزی واقعی (GET /AuditTrail) — نیازمند مجوز audit:read */
const actionLabels: Record<string, string> = {
  Create: 'ایجاد',
  Update: 'ویرایش',
  Submit: 'ارسال برای تأیید',
  Approve: 'تأیید',
  Reject: 'رد',
  Post: 'قطعی‌سازی',
  Reverse: 'معکوس‌سازی',
  Login: 'ورود',
  Logout: 'خروج',
};

export const RecentActivity: React.FC = () => {
  const { audit, canAudit } = useDashboardData({});
  const entries = audit.data ?? [];

  return (
    <WidgetShell
      title="فعالیت‌های اخیر"
      isLoading={audit.isLoading}
      isError={audit.isError}
      isUnauthorized={!canAudit}
      isEmpty={entries.length === 0}
      emptyMessage="فعالیتی ثبت نشده است"
      onRetry={() => void audit.refetch()}
    >
      <ol className="relative space-y-3 border-s border-border/60 ps-4">
        {entries.map((entry) => (
          <li key={entry.id} className="relative">
            <span className="absolute -start-[21px] top-1 size-2 rounded-full border-2 border-surface bg-primary" aria-hidden />
            <p className="text-xs text-foreground">
              <span className="font-semibold">{actionLabels[entry.action] ?? entry.action}</span>{' '}
              <span className="text-muted-foreground">روی {entry.entityType}</span>
            </p>
            <p className="mt-0.5 text-[10px] text-muted-foreground" dir="ltr">
              {entry.userId?.slice(0, 8)}… · {formatToPersianDateTime(entry.timestampUtc)}
            </p>
          </li>
        ))}
      </ol>
    </WidgetShell>
  );
};
