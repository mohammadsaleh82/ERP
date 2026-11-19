/**
 * ترازنامه — GET /accounting/reports/balance-sheet
 * همه بخش‌ها و جمع‌ها و IsBalanced/Difference از backend؛ frontend فقط نمایش.
 * در عدم توازن، گزارش صراحتاً «قطعی» معرفی نمی‌شود.
 */
import * as React from 'react';
import { PermissionGuard } from '@/app/guards/permission-guard';
import { PERMISSIONS } from '@/core/auth/permissions';

import { useQuery } from '@tanstack/react-query';
import { CheckCircle2, XCircle, Landmark } from 'lucide-react';
import { Card, CardContent, CardHeader } from '@/shared/components/ui/card';
import { BalanceCell } from '@/features/reports/components/financial-table';
import { ReportPageShell } from '@/features/reports/components/report-page-shell';
import { useReportFilters } from '@/features/reports/use-report-filters';
import { getBalanceSheet } from '@/core/accounting/statements.service';
import { formatAmount } from '@/core/accounting/documents.service';
import { formatToPersianDate } from '@/shared/utils/date';
import type { StatementSectionDto } from '@/core/accounting/statements.types';

const sectionRows = (section: StatementSectionDto | undefined) => section?.rows ?? [];

const SectionTable: React.FC<{ section: StatementSectionDto | undefined; label: string; testId: string }> = ({ section, label, testId }) => {
  const rows = sectionRows(section);
  return (
    <Card>
      <CardHeader className="border-b border-border/60 pb-2.5 text-sm font-bold">{label}</CardHeader>
      <CardContent className="p-0">
        {rows.length === 0 ? (
          <p className="py-6 text-center text-xs text-muted-foreground">حسابی در این بخش طبقه‌بندی نشده است</p>
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
                <td colSpan={2} className="px-2.5 py-2">جمع {label}</td>
                <td className="px-2.5 py-2 text-end tabular-nums" data-testid={`${testId}-total`}>{formatAmount(section?.total ?? '0')}</td>
              </tr>
            </tfoot>
          </table>
        )}
      </CardContent>
    </Card>
  );
};

const BalanceSheetContent: React.FC = () => {
  const { values, updateFilters, setPage, companyId } = useReportFilters();
  const [enabled, setEnabled] = React.useState(false);

  const report = useQuery({
    queryKey: ['reports', 'balance-sheet', companyId, values.fiscalYearId, values.fiscalPeriodId, values.to, values.costCenterId, values.projectId, values.currencyCode],
    queryFn: () => getBalanceSheet({
      fiscalYearId: values.fiscalYearId,
      fiscalPeriodId: values.fiscalPeriodId,
      to: values.to,
      costCenterId: values.costCenterId,
      projectId: values.projectId,
      currencyCode: values.currencyCode,
    }),
    enabled,
  });

  const summary = report.data ? (
    <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
      <div className="rounded-lg border border-border/60 bg-surface px-3 py-2 text-xs" data-testid="bs-total-assets">
        <span className="text-muted-foreground">جمع دارایی‌ها</span>
        <p className="mt-0.5 font-bold tabular-nums">{formatAmount(report.data.totalAssets)}</p>
      </div>
      <div className="rounded-lg border border-border/60 bg-surface px-3 py-2 text-xs" data-testid="bs-total-liabilities">
        <span className="text-muted-foreground">جمع بدهی‌ها</span>
        <p className="mt-0.5 font-bold tabular-nums">{formatAmount(report.data.totalLiabilities)}</p>
      </div>
      <div className="rounded-lg border border-border/60 bg-surface px-3 py-2 text-xs" data-testid="bs-total-equity">
        <span className="text-muted-foreground">جمع حقوق مالکانه</span>
        <p className="mt-0.5 font-bold tabular-nums">{formatAmount(report.data.totalEquity)}</p>
      </div>
      <div className="rounded-lg border border-border/60 bg-surface px-3 py-2 text-xs">
        <span className="text-muted-foreground">نتیجه دوره جاری</span>
        <p className="mt-0.5 font-bold tabular-nums">{formatAmount(report.data.currentPeriodResult)}</p>
      </div>
    </div>
  ) : null;

  return (
    <ReportPageShell
      title="ترازنامه"
      description={`وضعیت مالی شرکت تا تاریخ ${report.data ? formatToPersianDate(report.data.asOfDate) : 'انتخابی'} — بخش‌بندی و جمع‌ها از محاسبات backend بر پایه طبقه‌بندی ریشه‌های کدینگ`}
      icon={<Landmark className="size-5 text-primary" aria-hidden />}
      filters={{ values: values, companyId, updateFilters, setPage }}
      filterKinds={['year', 'dates', 'costCenter', 'project', 'currency']}
      onRun={() => setEnabled(true)}
      loading={report.isFetching}
      error={report.isError}
      onRetry={() => void report.refetch()}
      generatedAt={report.dataUpdatedAt || undefined}
      summary={summary}
      exportReportName="balancesheet"
      exportParams={{ fiscalYearId: values.fiscalYearId, fiscalPeriodId: values.fiscalPeriodId, to: values.to, costCenterId: values.costCenterId, projectId: values.projectId, currencyCode: values.currencyCode }}
      testIdPrefix="bs"
    >
      {report.data && (
        <div className={`flex items-center gap-2 rounded-md border px-3 py-2 text-xs ${report.data.isBalanced ? 'border-success/40 bg-success/5 text-success' : 'border-danger/40 bg-danger/5 text-danger'}`} data-testid="bs-balance-banner">
          {report.data.isBalanced ? <CheckCircle2 className="size-4" aria-hidden /> : <XCircle className="size-4" aria-hidden />}
          <span>
            {report.data.isBalanced
              ? 'ترازنامه تراز است — دارایی‌ها با بدهی‌ها و حقوق مالکانه برابر است. این گزارش قابل استناد است.'
              : `عدم توازن (اختلاف ${formatAmount(report.data.difference)}) — تا رفع طبقه‌بندی یا داده‌های ناقص، این گزارش قطعی نیست.`}
          </span>
        </div>
      )}
      <div className="grid gap-4 lg:grid-cols-2">
        <SectionTable section={report.data?.currentAssets} label="دارایی‌های جاری" testId="bs-current-assets" />
        <SectionTable section={report.data?.nonCurrentAssets} label="دارایی‌های غیرجاری" testId="bs-noncurrent-assets" />
        <SectionTable section={report.data?.currentLiabilities} label="بدهی‌های جاری" testId="bs-current-liabilities" />
        <SectionTable section={report.data?.nonCurrentLiabilities} label="بدهی‌های غیرجاری" testId="bs-noncurrent-liabilities" />
        <SectionTable section={report.data?.equity} label="حقوق مالکانه" testId="bs-equity" />
        {report.data && (
          <Card>
            <CardHeader className="border-b border-border/60 pb-2.5 text-sm font-bold">کنترل توازن</CardHeader>
            <CardContent className="p-0 text-xs">
              <div className="flex items-center justify-between border-b border-border/40 px-2.5 py-2">
                <span>جمع دارایی‌ها</span>
                <strong className="tabular-nums" data-testid="bs-eq-assets">{formatAmount(report.data.totalAssets)}</strong>
              </div>
              <div className="flex items-center justify-between border-b border-border/40 px-2.5 py-2">
                <span>جمع بدهی‌ها</span>
                <strong className="tabular-nums">{formatAmount(report.data.totalLiabilities)}</strong>
              </div>
              <div className="flex items-center justify-between border-b border-border/40 px-2.5 py-2">
                <span>جمع حقوق مالکانه</span>
                <strong className="tabular-nums">{formatAmount(report.data.totalEquity)}</strong>
              </div>
              <div className="flex items-center justify-between px-2.5 py-2">
                <span>اختلاف توازن (محاسبه backend)</span>
                <strong className="tabular-nums" data-testid="bs-difference">{formatAmount(report.data.difference)}</strong>
              </div>
            </CardContent>
          </Card>
        )}
      </div>
    </ReportPageShell>
  );
};

export const BalanceSheetPage: React.FC = () => (
  <PermissionGuard permissions={[PERMISSIONS.reportsRead]}>
    <BalanceSheetContent />
  </PermissionGuard>
);
