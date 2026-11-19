import * as React from 'react';
import { useSearchParams } from 'react-router-dom';
import { WelcomeBanner } from '@/features/dashboard/components/welcome-banner';
import { DateRangeFilter } from '@/features/dashboard/components/date-range-filter';
import { SummaryCards } from '@/features/dashboard/components/summary-cards';
import { DocumentsTrendChart } from '@/features/dashboard/components/documents-trend-chart';
import { PendingApprovals } from '@/features/dashboard/components/pending-approvals';
import { FiscalStatusWidget } from '@/features/dashboard/components/fiscal-status';
import { TopBalances } from '@/features/dashboard/components/top-balances';
import { RecentActivity } from '@/features/dashboard/components/recent-activity';
import { FinancialAlerts } from '@/features/dashboard/components/financial-alerts';
import { QuickActions } from '@/features/dashboard/components/quick-actions';

/**
 * داشبورد مدیریتی — همه داده‌ها واقعی و همه widgetها مستقل.
 * فیلتر بازه زمانی در URL (from/to) persist می‌شود تا state قابل بازگشت باشد.
 */
export const DashboardPage: React.FC = () => {
  const [searchParams, setSearchParams] = useSearchParams();

  const range = React.useMemo(
    () => ({
      from: searchParams.get('from') ?? undefined,
      to: searchParams.get('to') ?? undefined,
    }),
    [searchParams],
  );

  const handleRangeChange = React.useCallback(
    (next: { from?: string; to?: string }) => {
      const params = new URLSearchParams();
      if (next.from) params.set('from', next.from);
      if (next.to) params.set('to', next.to);
      // بازه حذف شده → بدون پرش صفحه، فقط جایگزینی query string
      setSearchParams(params, { replace: true });
    },
    [setSearchParams],
  );

  return (
    <div className="space-y-4" data-testid="dashboard-page">
      <WelcomeBanner />
      <DateRangeFilter from={range.from} to={range.to} onChange={handleRangeChange} />
      <SummaryCards range={range} />

      <div className="grid grid-cols-1 gap-3 lg:grid-cols-3">
        <DocumentsTrendChart range={range} />
        <FiscalStatusWidget />
        <FinancialAlerts />
      </div>

      <QuickActions />

      <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
        <PendingApprovals />
        <TopBalances range={range} />
      </div>

      <RecentActivity />
    </div>
  );
};
