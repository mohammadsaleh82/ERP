/**
 * بدهکاران و بستانکاران — GET /accounting/reports/receivables | /payables
 * مانده اول/گردش/مانده اشخاص (مشتریان روی حساب‌های دریافتنی، تأمین‌کنندگان روی
 * پرداختنی) همه از backend + بنر accountsConfigured و یادداشت رسمی.
 */
import * as React from 'react';
import { PermissionGuard } from '@/app/guards/permission-guard';
import { PERMISSIONS } from '@/core/auth/permissions';

import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, ArrowLeftRight, Users2 } from 'lucide-react';
import { Card, CardContent, CardHeader } from '@/shared/components/ui/card';
import { MoneyCell, BalanceCell, DescriptionCell } from '@/features/reports/components/financial-table';
import { ReportPageShell } from '@/features/reports/components/report-page-shell';
import { useReportFilters } from '@/features/reports/use-report-filters';
import { getReceivables, getPayables } from '@/core/accounting/statements.service';
import { formatAmount } from '@/core/accounting/documents.service';
import { digitsToPersian } from '@/shared/utils/number';
import type { PartyBalanceRowDto } from '@/core/accounting/statements.types';

export type PartyLedgerKind = 'receivables' | 'payables';

const PartyLedgerContent: React.FC<{ kind: PartyLedgerKind }> = ({ kind }) => {
  const { values, updateFilters, setPage, companyId } = useReportFilters();
  const [enabled, setEnabled] = React.useState(false);
  const isReceivables = kind === 'receivables';

  const report = useQuery({
    queryKey: ['reports', kind, companyId, values.fiscalYearId, values.fiscalPeriodId, values.from, values.to],
    queryFn: () => (isReceivables ? getReceivables : getPayables)({
      fiscalYearId: values.fiscalYearId,
      fiscalPeriodId: values.fiscalPeriodId,
      from: values.from,
      to: values.to,
    }),
    enabled,
  });

  const rows = report.data?.rows ?? [];

  const summary = report.data ? (
    <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
      <div className="rounded-lg border border-border/60 bg-surface px-3 py-2 text-xs" data-testid="party-total-debit">
        <span className="text-muted-foreground">جمع گردش بدهکار</span>
        <p className="mt-0.5 font-bold tabular-nums">{formatAmount(report.data.totalDebit)}</p>
      </div>
      <div className="rounded-lg border border-border/60 bg-surface px-3 py-2 text-xs" data-testid="party-total-credit">
        <span className="text-muted-foreground">جمع گردش بستانکار</span>
        <p className="mt-0.5 font-bold tabular-nums">{formatAmount(report.data.totalCredit)}</p>
      </div>
      <div className="rounded-lg border border-border/60 bg-surface px-3 py-2 text-xs" data-testid="party-count">
        <span className="text-muted-foreground">تعداد اشخاص</span>
        <p className="mt-0.5 font-bold">{digitsToPersian(rows.length)}</p>
      </div>
    </div>
  ) : null;

  return (
    <ReportPageShell
      title={report.data?.title ?? (isReceivables ? 'گزارش بدهکاران' : 'گزارش بستانکاران')}
      description={isReceivables
        ? 'مانده و گردش مشتریان روی حساب‌های دریافتنی — طبق طبقه‌بندی backend؛ جمع‌ها از سرور'
        : 'مانده و گردش تأمین‌کنندگان روی حساب‌های پرداختنی — طبق طبقه‌بندی backend؛ جمع‌ها از سرور'}
      icon={isReceivables ? <Users2 className="size-5 text-primary" aria-hidden /> : <ArrowLeftRight className="size-5 text-primary" aria-hidden />}
      filters={{ values: values, companyId, updateFilters, setPage }}
      filterKinds={['year', 'period', 'dates']}
      onRun={() => setEnabled(true)}
      loading={report.isFetching}
      error={report.isError}
      onRetry={() => void report.refetch()}
      generatedAt={report.dataUpdatedAt || undefined}
      summary={summary}
      exportReportName={isReceivables ? 'receivables' : 'payables'}
      exportParams={{ fiscalYearId: values.fiscalYearId, fiscalPeriodId: values.fiscalPeriodId, from: values.from, to: values.to }}
      testIdPrefix={isReceivables ? 'recv' : 'pay'}
    >
      {report.data && !report.data.accountsConfigured && (
        <p className="flex items-center gap-2 rounded-md border border-warning/40 bg-warning/5 px-3 py-2 text-xs text-warning" data-testid="party-unconfigured">
          <AlertTriangle className="size-3.5" aria-hidden />
          حساب‌های {isReceivables ? 'دریافتنی' : 'پرداختنی'} طبقه‌بندی نشده‌اند{report.data.note ? ` — ${report.data.note}` : ''}. گزارش قطعی نیست.
        </p>
      )}
      {report.data?.note && report.data.accountsConfigured && (
        <p className="rounded-md border border-border/60 bg-surface px-3 py-2 text-xs text-muted-foreground">{report.data.note}</p>
      )}

      <Card>
        <CardHeader className="border-b border-border/60 pb-2.5 text-sm font-bold">اشخاص و گردش آن‌ها</CardHeader>
        <CardContent className="p-0">
          {rows.length === 0 && report.data ? (
            <p className="py-8 text-center text-xs text-muted-foreground">شخصی با گردش در این بازه یافت نشد.</p>
          ) : (
            <div className="overflow-x-auto" data-testid="party-table">
              <table className="w-full text-xs" aria-label={report.data?.title ?? 'گردش اشخاص'}>
                <thead className="bg-muted/40 text-muted-foreground">
                  <tr>
                    <th scope="col" className="whitespace-nowrap px-2.5 py-2 text-start font-semibold">شخص</th>
                    <th scope="col" className="whitespace-nowrap px-2.5 py-2 text-end font-semibold">اول بدهکار</th>
                    <th scope="col" className="whitespace-nowrap px-2.5 py-2 text-end font-semibold">اول بستانکار</th>
                    <th scope="col" className="whitespace-nowrap px-2.5 py-2 text-end font-semibold">گردش بدهکار</th>
                    <th scope="col" className="whitespace-nowrap px-2.5 py-2 text-end font-semibold">گردش بستانکار</th>
                    <th scope="col" className="whitespace-nowrap px-2.5 py-2 text-end font-semibold">پایان بدهکار</th>
                    <th scope="col" className="whitespace-nowrap px-2.5 py-2 text-end font-semibold">پایان بستانکار</th>
                    <th scope="col" className="whitespace-nowrap px-2.5 py-2 text-end font-semibold">مانده خالص</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((row: PartyBalanceRowDto) => (
                    <tr key={row.dimensionId} className="border-t border-border/40 hover:bg-muted/30">
                      <td className="px-2.5 py-2 font-semibold"><DescriptionCell text={row.dimensionLabel} /></td>
                      <td className="px-2.5 py-2 text-end"><MoneyCell value={row.openingDebit} side="debit" /></td>
                      <td className="px-2.5 py-2 text-end"><MoneyCell value={row.openingCredit} side="credit" /></td>
                      <td className="px-2.5 py-2 text-end"><MoneyCell value={row.debitTurnover} side="debit" /></td>
                      <td className="px-2.5 py-2 text-end"><MoneyCell value={row.creditTurnover} side="credit" /></td>
                      <td className="px-2.5 py-2 text-end"><MoneyCell value={row.closingDebit} side="debit" /></td>
                      <td className="px-2.5 py-2 text-end"><MoneyCell value={row.closingCredit} side="credit" /></td>
                      <td className="px-2.5 py-2 text-end"><BalanceCell amount={row.signedBalance} /></td>
                    </tr>
                  ))}
                </tbody>
                <tfoot className="border-t-2 border-border bg-muted/30 font-bold">
                  <tr>
                    <td className="px-2.5 py-2">جمع کل (backend)</td>
                    <td colSpan={2} className="px-2.5 py-2" />
                    <td className="px-2.5 py-2 text-end tabular-nums" data-testid="party-footer-debit">{formatAmount(report.data?.totalDebit ?? '0')}</td>
                    <td className="px-2.5 py-2 text-end tabular-nums" data-testid="party-footer-credit">{formatAmount(report.data?.totalCredit ?? '0')}</td>
                    <td colSpan={3} className="px-2.5 py-2 text-[10px] text-muted-foreground">مجموع گردش‌ها از محاسبات سرور</td>
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

export const PartyLedgerPage: React.FC<{ kind: PartyLedgerKind }> = ({ kind }) => (
  <PermissionGuard permissions={[PERMISSIONS.reportsRead]}>
    <PartyLedgerContent kind={kind} />
  </PermissionGuard>
);
