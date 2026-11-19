import * as React from 'react';
import { WidgetShell } from '@/shared/components/widget/widget-shell';
import { useDashboardData, type DashboardRange } from '@/features/dashboard/dashboard.queries';
import { digitsToPersian } from '@/shared/utils/number';
import { formatToPersianDate } from '@/shared/utils/date';

/**
 * نمودار روند اسناد — صرفاً از DocumentDate اسناد واقعی بازه.
 * SVG ساده بدون کتابخانه خارجی؛ رتبه‌بندی روزها.
 */
export const DocumentsTrendChart: React.FC<{ range: DashboardRange }> = ({ range }) => {
  const { documents, canDocuments } = useDashboardData(range);

  const series = React.useMemo(() => {
    const byDate = new Map<string, number>();
    for (const doc of documents.data?.items ?? []) {
      const day = doc.documentDate?.slice(0, 10) ?? '';
      if (!day) continue;
      byDate.set(day, (byDate.get(day) ?? 0) + 1);
    }
    return Array.from(byDate.entries())
      .sort((a, b) => a[0].localeCompare(b[0]))
      .slice(-14);
  }, [documents.data]);

  const max = Math.max(1, ...series.map(([, count]) => count));

  return (
    <WidgetShell
      title="روند ثبت اسناد"
      className="min-h-[220px]"
      isLoading={documents.isLoading}
      isError={documents.isError}
      isUnauthorized={!canDocuments}
      isEmpty={series.length === 0}
      emptyMessage="سندی در بازه انتخابی ثبت نشده است"
      onRetry={() => void documents.refetch()}
      action={<span className="text-[11px] text-muted-foreground">۱۴ روز اخیر بازه</span>}
    >
      <div data-testid="documents-trend" className="flex h-36 items-end gap-1.5" role="img" aria-label="نمودار میله‌ای روند ثبت اسناد">
        {series.map(([day, count]) => (
          <div key={day} className="group flex min-w-0 flex-1 flex-col items-center gap-1">
            <span className="text-[10px] tabular-nums text-muted-foreground">{digitsToPersian(count)}</span>
            <div
              className="w-full rounded-t bg-primary/80 transition-all hover:bg-primary"
              style={{ height: `${Math.max(6, (count / max) * 100)}%` }}
              title={`${formatToPersianDate(day)} — ${digitsToPersian(count)} سند`}
            />
            <span className="w-full truncate text-center text-[9px] text-muted-foreground" dir="ltr">
              {day.slice(5)}
            </span>
          </div>
        ))}
      </div>
    </WidgetShell>
  );
};
