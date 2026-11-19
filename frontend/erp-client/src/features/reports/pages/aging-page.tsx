/**
 * گزارش سنی مطالبات — GET /accounting/reports/aging (فقط تا تاریخ)
 * سبد های سنی از محاسبات backend؛ در نبود سررسید معتبر (hasReliableDueDates=false)
 * گزارش با بنر هشدار «قطعی نیست» نمایش داده می‌شود. aging در endpoint خروجی نیست → فقط چاپ.
 */
import * as React from 'react';
import { PermissionGuard } from '@/app/guards/permission-guard';
import { PERMISSIONS } from '@/core/auth/permissions';

import { useQuery } from '@tanstack/react-query';
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip as ChartTooltip, XAxis, YAxis } from 'recharts';
import { AlertTriangle, Hourglass } from 'lucide-react';
import { Card, CardContent, CardHeader } from '@/shared/components/ui/card';
import { ReportPageShell } from '@/features/reports/components/report-page-shell';
import { useReportFilters } from '@/features/reports/use-report-filters';
import { getAging } from '@/core/accounting/statements.service';
import { formatAmount } from '@/core/accounting/documents.service';
import { formatToPersianDate } from '@/shared/utils/date';

const AgingContent: React.FC = () => {
  const { values, updateFilters, setPage, companyId } = useReportFilters();
  const [enabled, setEnabled] = React.useState(false);

  const report = useQuery({
    queryKey: ['reports', 'aging', companyId, values.to],
    queryFn: () => getAging(values.to),
    enabled,
  });

  const buckets = report.data?.buckets ?? [];
  const chartData = buckets.map((bucket) => ({ name: bucket.label, مبلغ: Number(bucket.amount) }));

  const summary = report.data ? (
    <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
      <div className="rounded-lg border border-border/60 bg-surface px-3 py-2 text-xs" data-testid="aging-total">
        <span className="text-muted-foreground">جمع مطالبات</span>
        <p className="mt-0.5 font-bold tabular-nums">{formatAmount(report.data.totalAmount)}</p>
      </div>
      <div className="rounded-lg border border-border/60 bg-surface px-3 py-2 text-xs">
        <span className="text-muted-foreground">تعداد سبد سنی</span>
        <p className="mt-0.5 font-bold">{buckets.length}</p>
      </div>
      <div className="rounded-lg border border-border/60 bg-surface px-3 py-2 text-xs">
        <span className="text-muted-foreground">تا تاریخ</span>
        <p className="mt-0.5 font-bold">{formatToPersianDate(report.data.asOfDate)}</p>
      </div>
    </div>
  ) : null;

  return (
    <ReportPageShell
      title="گزارش سنی مطالبات و بدهی‌ها"
      description={`تفکیک سنی مطالبات تا تاریخ ${report.data ? formatToPersianDate(report.data.asOfDate) : 'انتخابی'} — سبد‌ها بر پایه تاریخ سررسید از محاسبات backend`}
      icon={<Hourglass className="size-5 text-primary" aria-hidden />}
      filters={{ values: values, companyId, updateFilters, setPage }}
      filterKinds={['dates']}
      onRun={() => setEnabled(true)}
      loading={report.isFetching}
      error={report.isError}
      onRetry={() => void report.refetch()}
      generatedAt={report.dataUpdatedAt || undefined}
      summary={summary}
      testIdPrefix="aging"
    >
      {report.data && !report.data.hasReliableDueDates && (
        <p className="flex items-center gap-2 rounded-md border border-warning/40 bg-warning/5 px-3 py-2 text-xs text-warning" data-testid="aging-unreliable">
          <AlertTriangle className="size-3.5" aria-hidden />
          تاریخ سررسید قابل اتکا نیست{report.data.note ? ` — ${report.data.note}` : ''}. تفکیک سنی تقریبی است و گزارش قطعی محسوب نمی‌شود.
        </p>
      )}

      {chartData.length > 0 && (
        <Card className="report-no-print">
          <CardHeader className="border-b border-border/60 pb-2.5 text-sm font-bold">نمودار سبد‌های سنی — مقادیر مستقیم از API</CardHeader>
          <CardContent className="h-52 pt-3" data-testid="aging-chart">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={chartData} margin={{ top: 4, bottom: 4 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#94a3b855" />
                <XAxis dataKey="name" tick={{ fontSize: 10 }} />
                <YAxis tick={{ fontSize: 10 }} width={70} tickFormatter={(value) => formatAmount(Number(value))} />
                <ChartTooltip formatter={(value) => formatAmount(Number(value))} contentStyle={{ direction: 'rtl', fontSize: 12 }} />
                <Bar dataKey="مبلغ" name="مبلغ مطالبات" fill="#7c3aed" radius={[3, 3, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader className="border-b border-border/60 pb-2.5 text-sm font-bold">جدول سبد‌های سنی (backend)</CardHeader>
        <CardContent className="p-0">
          {buckets.length === 0 && report.data ? (
            <p className="py-8 text-center text-xs text-muted-foreground">سبدی برای نمایش نیست — مطالبات سررسیددار ثبت نشده است.</p>
          ) : (
            <div className="overflow-x-auto">
            <table className="w-full text-xs" aria-label="سبد‌های سنی" data-testid="aging-table">
              <thead className="bg-muted/40 text-muted-foreground">
                <tr>
                  <th scope="col" className="px-2.5 py-2 text-start font-semibold">سبد سنی</th>
                  <th scope="col" className="px-2.5 py-2 text-end font-semibold">مبلغ</th>
                  <th scope="col" className="px-2.5 py-2 text-end font-semibold">سهم از کل</th>
                </tr>
              </thead>
              <tbody>
                {buckets.map((bucket) => {
                  const share = Number(report.data!.totalAmount) > 0 ? (Number(bucket.amount) / Number(report.data!.totalAmount)) * 100 : 0;
                  return (
                    <tr key={bucket.label} className="border-t border-border/40 hover:bg-muted/30">
                      <td className="px-2.5 py-2 font-semibold">{bucket.label}</td>
                      <td className="px-2.5 py-2 text-end tabular-nums" data-testid={`aging-bucket-${bucket.label}`}>{formatAmount(bucket.amount)}</td>
                      <td className="px-2.5 py-2 text-end tabular-nums text-muted-foreground">{share.toFixed(1)}%</td>
                    </tr>
                  );
                })}
              </tbody>
              <tfoot className="border-t-2 border-border bg-muted/30 font-bold">
                <tr>
                  <td className="px-2.5 py-2">جمع کل (backend)</td>
                  <td className="px-2.5 py-2 text-end tabular-nums" data-testid="aging-footer-total">{formatAmount(report.data?.totalAmount ?? '0')}</td>
                  <td className="px-2.5 py-2 text-end text-[10px] text-muted-foreground">100%</td>
                </tr>
              </tfoot>
            </table>
            </div>
          )}
        </CardContent>
      </Card>
    </ReportPageShell>
  );
};

export const AgingPage: React.FC = () => (
  <PermissionGuard permissions={[PERMISSIONS.reportsRead]}>
    <AgingContent />
  </PermissionGuard>
);
