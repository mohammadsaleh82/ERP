/**
 * تراز آزمایشی — GET /accounting/reports/trial-balance
 * بنر توازن/عدم توازن، جمع کل و اختلاف از backend، سطح تجمیع و فیلتر ابعاد.
 */
import * as React from 'react';
import { useQuery } from '@tanstack/react-query';
import { CheckCircle2, XCircle, Sigma } from 'lucide-react';
import { Card, CardContent } from '@/shared/components/ui/card';
import { PermissionGuard } from '@/app/guards/permission-guard';
import { PERMISSIONS } from '@/core/auth/permissions';
import { getTrialBalance } from '@/core/accounting/reports.service';
import { ReportFilterBar } from '@/features/reports/components/report-filter-bar';
import { useReportFilters } from '@/features/reports/use-report-filters';
import { FinancialTable, MoneyCell, DescriptionCell } from '@/features/reports/components/financial-table';
import { formatAmount } from '@/core/accounting/documents.service';
import { formatToPersianDate } from '@/shared/utils/date';
import type { TrialBalanceRow } from '@/core/accounting/reports.types';

const levelLabel = (level: string): string => ({ Group: 'گروه', General: 'کل', Subsidiary: 'معین', Detail: 'تفصیلی' }[level] ?? level);

const TrialBalanceContent: React.FC = () => {
  const { values, updateFilters, companyId } = useReportFilters();

  const [accountLevel, setAccountLevel] = React.useState(values.accountLevel ?? 'Detail');

  const report = useQuery({
    queryKey: ['reports', 'trial-balance', companyId, values.fiscalYearId, values.fiscalPeriodId, values.from, values.to, values.costCenterId, values.projectId, values.partyId, values.currencyCode, accountLevel],
    queryFn: () => getTrialBalance({
      fiscalYearId: values.fiscalYearId,
      fiscalPeriodId: values.fiscalPeriodId,
      from: values.from,
      to: values.to,
      costCenterId: values.costCenterId,
      projectId: values.projectId,
      partyId: values.partyId,
      currencyCode: values.currencyCode,
      level: accountLevel,
    }),
  });

  return (
    <div className="space-y-4" data-testid="trial-balance-page">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-lg font-black text-foreground">
            <Sigma className="size-5 text-primary" aria-hidden />
            تراز آزمایشی
          </h1>
          {report.data && (
            <p className="mt-0.5 text-xs text-muted-foreground">
              از {formatToPersianDate(report.data.fromDate)} تا {formatToPersianDate(report.data.toDate)} · سطح {levelLabel(report.data.aggregationLevel)}
              {report.data.fiscalYearName && ` · ${report.data.fiscalYearName}`}
            </p>
          )}
        </div>
        <label className="flex items-center gap-1.5 text-xs font-semibold" htmlFor="tb-level">
          سطح تجمیع
          <select id="tb-level" value={accountLevel} onChange={(event) => setAccountLevel(event.target.value)} className="h-9 rounded-md border border-border bg-surface px-2 text-xs" data-testid="tb-level">
            <option value="Group">گروه</option>
            <option value="General">کل</option>
            <option value="Subsidiary">معین</option>
            <option value="Detail">تفصیلی</option>
          </select>
        </label>
      </div>

      <ReportFilterBar values={values} onValuesChange={updateFilters} enabled={['year', 'dates', 'costCenter', 'project', 'party', 'currency']} testIdPrefix="tb" />

      {report.data && (
        <div className={`flex items-center gap-2 rounded-md border px-3 py-2 text-xs ${report.data.isBalanced ? 'border-success/40 bg-success/5 text-success' : 'border-danger/40 bg-danger/5 text-danger'}`} data-testid="tb-balance-banner">
          {report.data.isBalanced ? <CheckCircle2 className="size-4" aria-hidden /> : <XCircle className="size-4" aria-hidden />}
          <span>
            {report.data.isBalanced ? 'تراز برقرار است — جمع بدهکار و بستانکار برابر است.' : 'عدم توازن! اختلاف باید بررسی شود.'}
            {' '}جمع بدهکار: <strong className="tabular-nums">{formatAmount(report.data.totalDebit)}</strong> · جمع بستانکار: <strong className="tabular-nums">{formatAmount(report.data.totalCredit)}</strong> · اختلاف: <strong className="tabular-nums" data-testid="tb-difference">{formatAmount(report.data.difference) === '—' ? '0' : formatAmount(report.data.difference)}</strong>
          </span>
        </div>
      )}

      <Card>
        <CardContent className="p-0">
          <FinancialTable<TrialBalanceRow>
            columns={[
              { key: 'code', header: 'کد حساب', render: (row) => <span dir="ltr" className="font-code text-[11px]">{row.accountCode}</span> },
              { key: 'name', header: 'عنوان حساب', render: (row) => <DescriptionCell text={row.accountName} /> },
              { key: 'level', header: 'سطح', render: (row) => levelLabel(row.level) },
              { key: 'op-d', header: 'اول بدهکار', align: 'end', render: (row) => <MoneyCell value={row.openingDebit} side="debit" /> },
              { key: 'op-c', header: 'اول بستانکار', align: 'end', render: (row) => <MoneyCell value={row.openingCredit} side="credit" /> },
              { key: 'turn-d', header: 'گردش بدهکار', align: 'end', render: (row) => <MoneyCell value={row.periodDebit} side="debit" /> },
              { key: 'turn-c', header: 'گردش بستانکار', align: 'end', render: (row) => <MoneyCell value={row.periodCredit} side="credit" /> },
              { key: 'cl-d', header: 'پایان بدهکار', align: 'end', render: (row) => <MoneyCell value={row.closingDebit} side="debit" /> },
              { key: 'cl-c', header: 'پایان بستانکار', align: 'end', render: (row) => <MoneyCell value={row.closingCredit} side="credit" /> },
            ]}
            rows={report.data?.rows}
            rowKey={(row) => row.accountCode}
            loading={report.isLoading}
            emptyTitle="ردیفی برای تراز آزمایشی یافت نشد"
            ariaLabel="تراز آزمایشی"
          />
        </CardContent>
      </Card>
    </div>
  );
};

export const TrialBalancePage: React.FC = () => (
  <PermissionGuard permissions={[PERMISSIONS.reportsRead]}>
    <TrialBalanceContent />
  </PermissionGuard>
);
