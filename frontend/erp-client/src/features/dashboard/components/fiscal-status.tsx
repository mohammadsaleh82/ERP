import * as React from 'react';
import { CalendarRange } from 'lucide-react';
import { Badge } from '@/shared/components/ui/badge';
import { WidgetShell } from '@/shared/components/widget/widget-shell';
import { useDashboardData } from '@/features/dashboard/dashboard.queries';
import { fiscalStatusToLabel } from '@/features/dashboard/components/status-labels';
import { digitsToPersian } from '@/shared/utils/number';
import { formatToPersianDate } from '@/shared/utils/date';
import { useCompanyStore } from '@/core/company/company.store';

/** وضعیت دوره مالی واقعی: سال باز + دوره‌های باز از FiscalYears/periods */
export const FiscalStatusWidget: React.FC = () => {
  const { fiscalYears, periods, canFiscal } = useDashboardData({});
  const activeCompany = useCompanyStore((state) => state.activeCompany);

  const openYear = fiscalYears.data?.find((year) => year.status === 'Open');
  const openPeriods = (periods.data ?? []).filter((period) => period.status === 'Open' && !period.isAdjustmentPeriod);
  const currentPeriod = openPeriods.find(
    (period) => period.startDate <= (new Date().toISOString().slice(0, 10)) && period.endDate >= (new Date().toISOString().slice(0, 10)),
  );

  return (
    <WidgetShell
      title="وضعیت دوره مالی"
      isLoading={fiscalYears.isLoading || periods.isLoading}
      isError={fiscalYears.isError || periods.isError}
      isUnauthorized={!canFiscal}
      isEmpty={!openYear}
      emptyMessage={fiscalYears.data && fiscalYears.data.length > 0 ? 'سال مالی باز وجود ندارد (سال در اختتامیه یا بسته است)' : 'سال مالی ثبت نشده است'}
      onRetry={() => void fiscalYears.refetch()}
    >
      <div className="space-y-3">
        <div className="flex items-center justify-between gap-2 rounded-md border bg-muted/30 px-3 py-2.5">
          <span className="flex items-center gap-2 text-sm font-bold text-foreground">
            <CalendarRange className="size-4 text-primary" aria-hidden />
            سال مالی {digitsToPersian(openYear?.yearNumber ?? activeCompany?.fiscalYear ?? 0)}
          </span>
          <Badge variant={openYear ? 'success' : 'secondary'}>{fiscalStatusToLabel(openYear?.status ?? 'Open')}</Badge>
        </div>
        <dl className="grid grid-cols-2 gap-2 text-xs">
          <div className="rounded-md border p-2.5">
            <dt className="text-muted-foreground">بازه سال</dt>
            <dd className="mt-0.5 font-medium text-foreground">
              {openYear ? `${formatToPersianDate(openYear.startDate)} — ${formatToPersianDate(openYear.endDate)}` : '—'}
            </dd>
          </div>
          <div className="rounded-md border p-2.5">
            <dt className="text-muted-foreground">دوره فعال</dt>
            <dd className="mt-0.5 font-medium text-foreground">
              {currentPeriod ? currentPeriod.name : openPeriods.length > 0 ? `${digitsToPersian(openPeriods.length)} دوره باز` : 'دوره باز ندارد'}
            </dd>
          </div>
        </dl>
      </div>
    </WidgetShell>
  );
};
