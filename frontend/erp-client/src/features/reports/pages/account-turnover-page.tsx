/**
 * گردش حساب (تجمیعی همه حساب‌ها) — GET /accounting/reports/general-ledger
 * ردیف هر حساب: مانده اول بدهکار/بستانکار، گردش بدهکار/بستانکار، مانده پایان بدهکار/بستانکار.
 * خروجی کامل است (backend لیست تجمیعی بدون pagination در این گزارش).
 */
import * as React from 'react';
import { useQuery } from '@tanstack/react-query';
import { TrendingUp } from 'lucide-react';
import { Card, CardContent } from '@/shared/components/ui/card';
import { PermissionGuard } from '@/app/guards/permission-guard';
import { PERMISSIONS } from '@/core/auth/permissions';
import { getReportsGeneralLedger } from '@/core/accounting/reports.service';
import { ReportFilterBar } from '@/features/reports/components/report-filter-bar';
import { useReportFilters } from '@/features/reports/use-report-filters';
import { FinancialTable, MoneyCell, TotalsFooter, DescriptionCell } from '@/features/reports/components/financial-table';
import { sumAmounts } from '@/core/accounting/documents.service';
import type { GeneralLedgerRowDto } from '@/core/accounting/reports.types';

const TurnoverContent: React.FC = () => {
  const { values, updateFilters, companyId } = useReportFilters();

  const report = useQuery({
    queryKey: ['reports', 'turnover-aggregate', companyId, values.fiscalYearId, values.fiscalPeriodId, values.from, values.to, values.accountLevel, values.accountId, values.costCenterId, values.projectId, values.partyId, values.currencyCode],
    queryFn: () => getReportsGeneralLedger({
      fiscalYearId: values.fiscalYearId,
      fiscalPeriodId: values.fiscalPeriodId,
      from: values.from,
      to: values.to,
      accountId: values.accountId,
      accountLevel: values.accountLevel,
      partyId: values.partyId,
      costCenterId: values.costCenterId,
      projectId: values.projectId,
      currencyCode: values.currencyCode,
    }),
  });

  const rows = report.data ?? [];
  // جمع‌ها با decimal-safe (فقط بازخورد نمایشی؛ موتور backend مرجع است)
  const totals = {
    openingDebit: sumAmounts(rows.map((row) => row.openingDebit)),
    openingCredit: sumAmounts(rows.map((row) => row.openingCredit)),
    debit: sumAmounts(rows.map((row) => row.debitTurnover)),
    credit: sumAmounts(rows.map((row) => row.creditTurnover)),
    closingDebit: sumAmounts(rows.map((row) => row.closingDebit)),
    closingCredit: sumAmounts(rows.map((row) => row.closingCredit)),
  };

  return (
    <div className="space-y-4" data-testid="account-turnover-page">
      <div>
        <h1 className="flex items-center gap-2 text-lg font-black text-foreground">
          <TrendingUp className="size-5 text-primary" aria-hidden />
          گردش حساب
        </h1>
        <p className="mt-0.5 text-xs text-muted-foreground">مانده اول، گردش دوره و مانده پایان به تفکیک بدهکار/بستانکار — از گزارش‌های مالی backend</p>
      </div>

      <ReportFilterBar values={values} onValuesChange={updateFilters} enabled={['year', 'dates', 'detail', 'costCenter', 'project', 'party', 'currency']} testIdPrefix="turn" />

      <Card>
        <CardContent className="p-0">
          <FinancialTable<GeneralLedgerRowDto>
            columns={[
              { key: 'code', header: 'کد حساب', render: (row) => <span dir="ltr" className="font-code text-[11px]">{row.accountCode}</span> },
              { key: 'name', header: 'عنوان حساب', render: (row) => <DescriptionCell text={row.accountName} /> },
              { key: 'op-d', header: 'اول بدهکار', align: 'end', render: (row) => <MoneyCell value={row.openingDebit} side="debit" /> },
              { key: 'op-c', header: 'اول بستانکار', align: 'end', render: (row) => <MoneyCell value={row.openingCredit} side="credit" /> },
              { key: 'turn-d', header: 'گردش بدهکار', align: 'end', render: (row) => <MoneyCell value={row.debitTurnover} side="debit" /> },
              { key: 'turn-c', header: 'گردش بستانکار', align: 'end', render: (row) => <MoneyCell value={row.creditTurnover} side="credit" /> },
              { key: 'cl-d', header: 'پایان بدهکار', align: 'end', render: (row) => <MoneyCell value={row.closingDebit} side="debit" /> },
              { key: 'cl-c', header: 'پایان بستانکار', align: 'end', render: (row) => <MoneyCell value={row.closingCredit} side="credit" /> },
            ]}
            rows={rows}
            rowKey={(row) => row.accountId}
            loading={report.isLoading}
            emptyTitle="گردشی در این بازه یافت نشد"
            ariaLabel="گردش حساب"
            footer={(
              <TotalsFooter
                colSpanBefore={2}
                cells={[
                  <MoneyCell key="1" value={totals.openingDebit} side="debit" />,
                  <MoneyCell key="2" value={totals.openingCredit} side="credit" />,
                  <MoneyCell key="3" value={totals.debit} side="debit" testId="turn-total-debit" />,
                  <MoneyCell key="4" value={totals.credit} side="credit" testId="turn-total-credit" />,
                  <MoneyCell key="5" value={totals.closingDebit} side="debit" />,
                  <MoneyCell key="6" value={totals.closingCredit} side="credit" />,
                ]}
              />
            )}
          />
        </CardContent>
      </Card>
    </div>
  );
};

export const AccountTurnoverPage: React.FC = () => (
  <PermissionGuard permissions={[PERMISSIONS.reportsRead]}>
    <TurnoverContent />
  </PermissionGuard>
);
