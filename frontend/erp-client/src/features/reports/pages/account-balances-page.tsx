/**
 * مانده حساب — GET /accounting/ledger/balances (موتور مانده backend)
 * Opening/Period/Closing با جهت واقعی و پرچم مانده غیرعادی + فیلتر حساب/سال/دوره/بازه.
 */
import * as React from 'react';
import { useQuery } from '@tanstack/react-query';
import { Scale } from 'lucide-react';
import { Card, CardContent } from '@/shared/components/ui/card';
import { PermissionGuard } from '@/app/guards/permission-guard';
import { PERMISSIONS } from '@/core/auth/permissions';
import { getLedgerBalances } from '@/core/accounting/reports.service';
import { ReportFilterBar } from '@/features/reports/components/report-filter-bar';
import { useReportFilters } from '@/features/reports/use-report-filters';
import { FinancialTable, BalanceCell, MoneyCell, DescriptionCell } from '@/features/reports/components/financial-table';
import type { AccountBalanceResultDto } from '@/core/accounting/reports.types';

const BalancesContent: React.FC = () => {
  const { values, updateFilters, companyId } = useReportFilters();

  const balances = useQuery({
    queryKey: ['reports', 'balances', companyId, values.fiscalYearId, values.fiscalPeriodId, values.from, values.to, values.accountId],
    queryFn: () => getLedgerBalances({
      accountId: values.accountId,
      fiscalYearId: values.fiscalYearId,
      periodId: values.fiscalPeriodId,
      fromDate: values.from,
      toDate: values.to,
    }),
  });

  return (
    <div className="space-y-4" data-testid="balances-page">
      <div>
        <h1 className="flex items-center gap-2 text-lg font-black text-foreground">
          <Scale className="size-5 text-primary" aria-hidden />
          مانده حساب
        </h1>
        <p className="mt-0.5 text-xs text-muted-foreground">مانده‌ها از موتور مانده backend — مانده خلاف ماهیت با علامت هشدار نشان داده می‌شود</p>
      </div>

      <ReportFilterBar values={values} onValuesChange={updateFilters} enabled={['account', 'year', 'period', 'dates']} testIdPrefix="bal" />

      <Card>
        <CardContent className="p-0">
          <FinancialTable<AccountBalanceResultDto>
            columns={[
              { key: 'code', header: 'کد حساب', render: (row) => <span dir="ltr" className="font-code text-[11px]">{row.accountFullCode}</span> },
              { key: 'name', header: 'عنوان حساب', render: (row) => <DescriptionCell text={row.accountName} /> },
              { key: 'nature', header: 'ماهیت', render: (row) => (row.normalBalance === 'Debit' ? 'بدهکار' : 'بستانکار') },
              { key: 'opening', header: 'مانده اول دوره', align: 'end', render: (row) => <BalanceCell amount={row.openingDisplayBalance} direction={row.openingDirection} abnormal={row.isAbnormalOpeningBalance} testId="bal-opening" /> },
              { key: 'debit', header: 'گردش بدهکار', align: 'end', render: (row) => <MoneyCell value={row.periodDebit} side="debit" /> },
              { key: 'credit', header: 'گردش بستانکار', align: 'end', render: (row) => <MoneyCell value={row.periodCredit} side="credit" /> },
              { key: 'closing', header: 'مانده پایان دوره', align: 'end', render: (row) => <BalanceCell amount={row.closingDisplayBalance} direction={row.closingDirection} abnormal={row.isAbnormalClosingBalance} testId="bal-closing" /> },
            ]}
            rows={balances.data}
            rowKey={(row) => row.accountId}
            loading={balances.isLoading}
            emptyTitle="مانده‌ای یافت نشد"
            ariaLabel="مانده حساب"
          />
        </CardContent>
      </Card>
    </div>
  );
};

export const AccountBalancesPage: React.FC = () => (
  <PermissionGuard permissions={[PERMISSIONS.ledgerRead]}>
    <BalancesContent />
  </PermissionGuard>
);
