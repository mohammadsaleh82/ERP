/**
 * دفتر معین — GET /accounting/ledger/subsidiary-ledger (حساب کل والد الزامی)
 * حساب‌های زیرمجموعه با مانده اول/گردش/مانده + جمع کل بدون شمارش دوباره backend.
 */
import * as React from 'react';
import { useQuery } from '@tanstack/react-query';
import { ListTree } from 'lucide-react';
import { Card, CardContent } from '@/shared/components/ui/card';
import { PermissionGuard } from '@/app/guards/permission-guard';
import { PERMISSIONS } from '@/core/auth/permissions';
import { getLedgerSubsidiary } from '@/core/accounting/reports.service';
import { ReportFilterBar } from '@/features/reports/components/report-filter-bar';
import { useReportFilters } from '@/features/reports/use-report-filters';
import { FinancialTable, MoneyCell, BalanceCell, TotalsFooter, DescriptionCell } from '@/features/reports/components/financial-table';

const SubsidiaryContent: React.FC = () => {
  const { values, updateFilters, companyId } = useReportFilters();

  const report = useQuery({
    queryKey: ['reports', 'subsidiary', companyId, values.parentAccountId, values.fiscalYearId, values.from, values.to],
    queryFn: () => getLedgerSubsidiary({
      parentAccountId: values.parentAccountId!,
      fiscalYearId: values.fiscalYearId,
      fromDate: values.from,
      toDate: values.to,
    }),
    enabled: Boolean(values.parentAccountId),
  });

  return (
    <div className="space-y-4" data-testid="subsidiary-ledger-page">
      <div>
        <h1 className="flex items-center gap-2 text-lg font-black text-foreground">
          <ListTree className="size-5 text-primary" aria-hidden />
          دفتر معین
        </h1>
        <p className="mt-0.5 text-xs text-muted-foreground">گردش حساب‌های زیرمجموعه یک حساب کل — جمع‌ها از backend (بدون محاسبه مجدد)</p>
      </div>

      <ReportFilterBar values={values} onValuesChange={updateFilters} enabled={['parentAccount', 'year', 'dates']} testIdPrefix="sub" />

      {!values.parentAccountId ? (
        <Card>
          <CardContent className="py-10 text-center text-sm text-muted-foreground">
            حساب کل (والد) را انتخاب کنید تا حساب‌های معین آن نمایش داده شود.
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardContent className="p-0">
            <FinancialTable
              columns={[
                { key: 'code', header: 'کد حساب', render: (row) => <span dir="ltr" className="font-code text-[11px]">{row.accountFullCode}</span> },
                { key: 'name', header: 'عنوان حساب', render: (row) => <DescriptionCell text={row.accountName} /> },
                { key: 'opening', header: 'مانده اول دوره', align: 'end', render: (row) => <BalanceCell amount={row.openingBalance} /> },
                { key: 'debit', header: 'گردش بدهکار', align: 'end', render: (row) => <MoneyCell value={row.periodDebit} side="debit" /> },
                { key: 'credit', header: 'گردش بستانکار', align: 'end', render: (row) => <MoneyCell value={row.periodCredit} side="credit" /> },
                { key: 'closing', header: 'مانده پایان', align: 'end', render: (row) => <BalanceCell amount={row.closingDisplayBalance} direction={row.closingDirection} abnormal={row.isAbnormalClosingBalance} /> },
              ]}
              rows={report.data?.subsidiaryAccounts}
              rowKey={(row) => row.accountId}
              loading={report.isLoading}
              emptyTitle="حساب معینی زیرمجموعه این حساب کل نیست"
              ariaLabel="دفتر معین"
              footer={(
                <TotalsFooter
                  colSpanBefore={3}
                  cells={[
                    <MoneyCell key="d" value={report.data?.totalPeriodDebit ?? '0'} side="debit" testId="sub-total-debit" />,
                    <MoneyCell key="c" value={report.data?.totalPeriodCredit ?? '0'} side="credit" testId="sub-total-credit" />,
                    <span key="b" />,
                  ]}
                />
              )}
            />
          </CardContent>
        </Card>
      )}
    </div>
  );
};

export const SubsidiaryLedgerPage: React.FC = () => (
  <PermissionGuard permissions={[PERMISSIONS.ledgerRead]}>
    <SubsidiaryContent />
  </PermissionGuard>
);
