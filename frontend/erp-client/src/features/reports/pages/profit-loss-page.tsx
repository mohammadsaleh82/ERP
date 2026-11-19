/**
 * سود و زیان — GET /accounting/reports/profit-loss
 * بخش‌ها (درآمد/بهای تمام‌شده/هزینه/سایر/مالیات) و سودهای مرحله‌ای همه از backend.
 * نمودار ستونی فقط برای درک سریع — مرجع دقیق، جدول است.
 */
import * as React from 'react';
import { PermissionGuard } from '@/app/guards/permission-guard';
import { PERMISSIONS } from '@/core/auth/permissions';

import { useQuery } from '@tanstack/react-query';
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip as ChartTooltip, XAxis, YAxis } from 'recharts';
import { TrendingUp } from 'lucide-react';
import { Card, CardContent, CardHeader } from '@/shared/components/ui/card';
import { BalanceCell } from '@/features/reports/components/financial-table';
import { ReportPageShell } from '@/features/reports/components/report-page-shell';
import { useReportFilters } from '@/features/reports/use-report-filters';
import { getProfitLoss } from '@/core/accounting/statements.service';
import { formatAmount } from '@/core/accounting/documents.service';
import { formatToPersianDate } from '@/shared/utils/date';
import type { StatementSectionDto } from '@/core/accounting/statements.types';

const SectionBlock: React.FC<{ section: StatementSectionDto | undefined; label: string; total?: string; totalLabel?: string; testId: string }> = ({ section, label, total, totalLabel, testId }) => {
  const rows = section?.rows ?? [];
  return (
    <Card>
      <CardHeader className="flex items-center justify-between border-b border-border/60 pb-2.5 text-sm font-bold">
        {label}
        {rows.length > 0 && <span className="text-[11px] font-normal text-muted-foreground">{rows.length} حساب</span>}
      </CardHeader>
      <CardContent className="p-0">
        {rows.length === 0 ? (
          <p className="py-5 text-center text-xs text-muted-foreground">حسابی در این بخش نیست</p>
        ) : (
          <table className="w-full text-xs" aria-label={label} data-testid={testId}>
            <thead className="bg-muted/40 text-muted-foreground">
              <tr>
                <th scope="col" className="px-2.5 py-2 text-start font-semibold">کد</th>
                <th scope="col" className="px-2.5 py-2 text-start font-semibold">حساب</th>
                <th scope="col" className="px-2.5 py-2 text-end font-semibold">مانده</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.accountId} className="border-t border-border/40 hover:bg-muted/30">
                  <td className="px-2.5 py-2"><span dir="ltr" className="font-code text-[11px]">{row.accountCode}</span></td>
                  <td className="px-2.5 py-2">{row.accountName}</td>
                  <td className="px-2.5 py-2 text-end"><BalanceCell amount={row.balance} /></td>
                </tr>
              ))}
            </tbody>
            <tfoot className="border-t-2 border-border bg-muted/30 font-bold">
              <tr>
                <td colSpan={2} className="px-2.5 py-2">{totalLabel ?? `جمع ${label}`}</td>
                <td className="px-2.5 py-2 text-end tabular-nums" data-testid={`${testId}-total`}>{formatAmount(total ?? section?.total ?? '0')}</td>
              </tr>
            </tfoot>
          </table>
        )}
      </CardContent>
    </Card>
  );
};

const StageRow: React.FC<{ label: string; value: string; testId: string; tone?: 'normal' | 'strong' }> = ({ label, value, testId, tone = 'normal' }) => (
  <div className={`flex items-center justify-between border-b border-border/40 px-2.5 py-2 ${tone === 'strong' ? 'bg-muted/20' : ''}`}>
    <span>{label}</span>
    <strong className="tabular-nums" data-testid={testId}>{formatAmount(value)}</strong>
  </div>
);

const ProfitLossContent: React.FC = () => {
  const { values, updateFilters, setPage, companyId } = useReportFilters();
  const [enabled, setEnabled] = React.useState(false);

  const report = useQuery({
    queryKey: ['reports', 'profit-loss', companyId, values.fiscalYearId, values.fiscalPeriodId, values.from, values.to, values.costCenterId, values.projectId, values.currencyCode],
    queryFn: () => getProfitLoss({
      fiscalYearId: values.fiscalYearId,
      fiscalPeriodId: values.fiscalPeriodId,
      from: values.from,
      to: values.to,
      costCenterId: values.costCenterId,
      projectId: values.projectId,
      currencyCode: values.currencyCode,
    }),
    enabled,
  });

  /* نمودار فقط از مقادیر API — بازه عددی برای رسم */
  const chartData = report.data ? [
    { name: 'درآمد', value: Number(report.data.revenues.total) },
    { name: 'بهای فروش', value: Number(report.data.costOfGoodsSold.total) },
    { name: 'سود ناخالص', value: Number(report.data.grossProfit) },
    { name: 'هزینه عملیاتی', value: Number(report.data.operatingExpenses.total) },
    { name: 'سود عملیاتی', value: Number(report.data.operatingProfit) },
    { name: 'سود قبل مالیات', value: Number(report.data.profitBeforeTax) },
    { name: 'مالیات', value: Number(report.data.taxExpenses.total) },
    { name: 'سود خالص', value: Number(report.data.netProfit) },
  ] : [];

  const summary = report.data ? (
    <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
      <div className="rounded-lg border border-border/60 bg-surface px-3 py-2 text-xs" data-testid="pl-gross">
        <span className="text-muted-foreground">سود ناخالص</span>
        <p className="mt-0.5 font-bold tabular-nums">{formatAmount(report.data.grossProfit)}</p>
      </div>
      <div className="rounded-lg border border-border/60 bg-surface px-3 py-2 text-xs" data-testid="pl-operating">
        <span className="text-muted-foreground">سود عملیاتی</span>
        <p className="mt-0.5 font-bold tabular-nums">{formatAmount(report.data.operatingProfit)}</p>
      </div>
      <div className="rounded-lg border border-border/60 bg-surface px-3 py-2 text-xs" data-testid="pl-pre-tax">
        <span className="text-muted-foreground">سود قبل از مالیات</span>
        <p className="mt-0.5 font-bold tabular-nums">{formatAmount(report.data.profitBeforeTax)}</p>
      </div>
      <div className="rounded-lg border border-primary/40 bg-primary/5 px-3 py-2 text-xs" data-testid="pl-net">
        <span className="text-muted-foreground">سود خالص</span>
        <p className="mt-0.5 font-bold tabular-nums">{formatAmount(report.data.netProfit)}</p>
      </div>
    </div>
  ) : null;

  return (
    <ReportPageShell
      title="صورت سود و زیان"
      description={`عملکرد دوره ${report.data ? `${formatToPersianDate(report.data.fromDate)} تا ${formatToPersianDate(report.data.toDate)}` : 'انتخابی'} — سودهای مرحله‌ای و بخش‌بندی حساب‌ها از محاسبات backend`}
      icon={<TrendingUp className="size-5 text-primary" aria-hidden />}
      filters={{ values: values, companyId, updateFilters, setPage }}
      filterKinds={['year', 'period', 'dates', 'costCenter', 'project', 'currency']}
      onRun={() => setEnabled(true)}
      loading={report.isFetching}
      error={report.isError}
      onRetry={() => void report.refetch()}
      generatedAt={report.dataUpdatedAt || undefined}
      summary={summary}
      exportReportName="profitloss"
      exportParams={{ fiscalYearId: values.fiscalYearId, fiscalPeriodId: values.fiscalPeriodId, from: values.from, to: values.to, costCenterId: values.costCenterId, projectId: values.projectId, currencyCode: values.currencyCode }}
      testIdPrefix="pl"
    >
      {chartData.length > 0 && (
        <Card className="report-no-print">
          <CardHeader className="border-b border-border/60 pb-2.5 text-sm font-bold">نمودار مراحل سود — مقادیر مستقیم از API (مرجع دقیق: جدول‌ها)</CardHeader>
          <CardContent className="h-56 pt-3" data-testid="pl-chart">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={chartData} margin={{ top: 4, bottom: 4 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#94a3b855" />
                <XAxis dataKey="name" tick={{ fontSize: 10 }} interval={0} angle={-18} textAnchor="end" height={46} />
                <YAxis tick={{ fontSize: 10 }} width={70} tickFormatter={(value) => formatAmount(Number(value))} />
                <ChartTooltip formatter={(value) => formatAmount(Number(value))} contentStyle={{ direction: 'rtl', fontSize: 12 }} />
                <Bar dataKey="value" name="مبلغ" fill="#2563eb" radius={[3, 3, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>
      )}

      <div className="grid gap-4 lg:grid-cols-2">
        <SectionBlock section={report.data?.revenues} label="درآمد عملیاتی" testId="pl-revenues" />
        <SectionBlock section={report.data?.costOfGoodsSold} label="بهای تمام‌شده کالای فروش‌رفته" testId="pl-cogs" />
        <Card>
          <CardHeader className="border-b border-border/60 pb-2.5 text-sm font-bold">مراحل سود</CardHeader>
          <CardContent className="p-0 text-xs">
            <StageRow label="سود ناخالص" value={report.data?.grossProfit ?? '0'} testId="pl-stage-gross" />
            <StageRow label="سود عملیاتی" value={report.data?.operatingProfit ?? '0'} testId="pl-stage-operating" tone="strong" />
            <StageRow label="سود قبل از مالیات" value={report.data?.profitBeforeTax ?? '0'} testId="pl-stage-pre-tax" />
            <StageRow label="سود خالص" value={report.data?.netProfit ?? '0'} testId="pl-stage-net" tone="strong" />
          </CardContent>
        </Card>
        <SectionBlock section={report.data?.operatingExpenses} label="هزینه‌های عملیاتی" testId="pl-opex" />
        <SectionBlock section={report.data?.otherIncome} label="درآمدهای غیرعملیاتی" testId="pl-other-income" />
        <SectionBlock section={report.data?.otherExpenses} label="هزینه‌های غیرعملیاتی" testId="pl-other-expense" />
        <SectionBlock section={report.data?.taxExpenses} label="هزینه مالیات" testId="pl-tax" />
      </div>
    </ReportPageShell>
  );
};

export const ProfitLossPage: React.FC = () => (
  <PermissionGuard permissions={[PERMISSIONS.reportsRead]}>
    <ProfitLossContent />
  </PermissionGuard>
);
