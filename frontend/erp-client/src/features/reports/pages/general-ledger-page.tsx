/**
 * دفتر کل — GET /accounting/ledger/general-ledger (صفحه‌بندی واقعی روی حساب‌ها)
 * مانده اول دوره، گردش بدهکار/بستانکار و مانده پایان دوره هر حساب + جمع دوره.
 * رفتن به گردش تفصیلی حساب با کلیک روی ردیف.
 */
import * as React from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ChevronLeft, ChevronRight, Layers } from 'lucide-react';
import { Card, CardContent } from '@/shared/components/ui/card';
import { Button } from '@/shared/components/ui/button';
import { PermissionGuard } from '@/app/guards/permission-guard';
import { PERMISSIONS } from '@/core/auth/permissions';
import { getLedgerGeneral } from '@/core/accounting/reports.service';
import { ReportFilterBar } from '@/features/reports/components/report-filter-bar';
import { useReportFilters } from '@/features/reports/use-report-filters';
import { FinancialTable, MoneyCell, BalanceCell, TotalsFooter, DescriptionCell } from '@/features/reports/components/financial-table';
import { digitsToPersian } from '@/shared/utils/number';
import type { AccountLedgerSummaryDto } from '@/core/accounting/reports.types';

const PAGE_SIZE = 25;

const GeneralLedgerContent: React.FC = () => {
  const navigate = useNavigate();
  const { values, updateFilters, setPage, companyId } = useReportFilters();
  const page = values.page ?? 1;

  const report = useQuery({
    queryKey: ['reports', 'general-ledger', companyId, values.fiscalYearId, values.from, values.to, values.accountId, page],
    queryFn: () => getLedgerGeneral({
      fiscalYearId: values.fiscalYearId,
      fromDate: values.from,
      toDate: values.to,
      accountId: values.accountId,
      pageNumber: page,
      pageSize: PAGE_SIZE,
    }),
  });

  const rows = report.data?.accounts ?? [];
  const totalCount = report.data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <div className="space-y-4" data-testid="general-ledger-page">
      <div>
        <h1 className="flex items-center gap-2 text-lg font-black text-foreground">
          <Layers className="size-5 text-primary" aria-hidden />
          دفتر کل
        </h1>
        <p className="mt-0.5 text-xs text-muted-foreground">خلاصه گردش و مانده حساب‌ها در بازه — برای گردش سطر به سطر، ردیف را باز کنید</p>
      </div>

      <ReportFilterBar values={values} onValuesChange={updateFilters} enabled={['year', 'dates', 'account']} testIdPrefix="gl" />

      <Card>
        <CardContent className="p-0">
          <FinancialTable<AccountLedgerSummaryDto>
            columns={[
              { key: 'code', header: 'کد حساب', render: (row) => <span dir="ltr" className="font-code text-[11px]">{row.accountFullCode}</span> },
              { key: 'name', header: 'عنوان حساب', render: (row) => <DescriptionCell text={row.accountName} /> },
              { key: 'opening', header: 'مانده اول دوره', align: 'end', render: (row) => <BalanceCell amount={row.openingBalance} testId="gl-opening" /> },
              { key: 'debit', header: 'گردش بدهکار', align: 'end', render: (row) => <MoneyCell value={row.periodDebit} side="debit" /> },
              { key: 'credit', header: 'گردش بستانکار', align: 'end', render: (row) => <MoneyCell value={row.periodCredit} side="credit" /> },
              { key: 'closing', header: 'مانده پایان دوره', align: 'end', render: (row) => <BalanceCell amount={row.closingDisplayBalance} direction={row.closingDirection} abnormal={row.isAbnormalClosingBalance} testId="gl-closing" /> },
              { key: 'actions', header: '', align: 'end', render: (row) => (
                <Button variant="ghost" size="sm" className="h-7 text-[11px]" onClick={() => navigate(`/accounting/reports/general-ledger/${row.accountId}?fiscalYearId=${values.fiscalYearId ?? ''}&from=${values.from ?? ''}&to=${values.to ?? ''}`)} data-testid={`gl-transactions-${row.accountFullCode}`}>
                  گردش تفصیلی
                </Button>
              ) },
            ]}
            rows={rows}
            rowKey={(row) => row.accountId}
            loading={report.isLoading}
            emptyTitle="گردشی در این بازه یافت نشد"
            ariaLabel="دفتر کل"
            footer={(
              <TotalsFooter
                colSpanBefore={3}
                cells={[
                  <MoneyCell key="d" value={report.data?.totalPeriodDebit ?? '0'} side="debit" testId="gl-total-debit" />,
                  <MoneyCell key="c" value={report.data?.totalPeriodCredit ?? '0'} side="credit" testId="gl-total-credit" />,
                  <span key="b" />,
                ]}
              />
            )}
          />
        </CardContent>
      </Card>

      <div className="flex items-center justify-between gap-2">
        <span className="text-xs text-muted-foreground">{digitsToPersian(totalCount)} حساب · صفحه {digitsToPersian(page)} از {digitsToPersian(totalPages)}</span>
        <div className="flex items-center gap-1">
          <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)} aria-label="صفحه قبل">
            <ChevronRight className="size-3.5" aria-hidden />
          </Button>
          <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)} aria-label="صفحه بعد">
            <ChevronLeft className="size-3.5" aria-hidden />
          </Button>
        </div>
      </div>
    </div>
  );
};

export const GeneralLedgerPage: React.FC = () => (
  <PermissionGuard permissions={[PERMISSIONS.ledgerRead]}>
    <GeneralLedgerContent />
  </PermissionGuard>
);
