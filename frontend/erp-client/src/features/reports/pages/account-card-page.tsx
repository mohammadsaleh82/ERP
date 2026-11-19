/**
 * کارت حساب — GET /accounting/reports/account-card/{accountId}
 * انتخاب حساب + فیلترهای وابسته (سال/دوره/بازه/تفصیلی/مرکز هزینه/پروژه/شخص/ارز)؛
 * مانده اول دوره + جهت، ردیف‌ها با مانده تراکمی (RunningBalance) و جهت، مانده پایان.
 */
import * as React from 'react';
import { useQuery } from '@tanstack/react-query';
import { ChevronLeft, ChevronRight, NotebookText } from 'lucide-react';
import { Card, CardContent, CardHeader } from '@/shared/components/ui/card';
import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { PermissionGuard } from '@/app/guards/permission-guard';
import { PERMISSIONS } from '@/core/auth/permissions';
import { getAccountCard } from '@/core/accounting/reports.service';
import { ReportFilterBar } from '@/features/reports/components/report-filter-bar';
import { useReportFilters } from '@/features/reports/use-report-filters';
import { FinancialTable, MoneyCell, BalanceCell, TotalsFooter, DocNumberCell, DescriptionCell } from '@/features/reports/components/financial-table';
import { formatAmount } from '@/core/accounting/documents.service';
import { formatToPersianDate } from '@/shared/utils/date';
import { digitsToPersian } from '@/shared/utils/number';

const PAGE_SIZE = 30;

const sideLabel = (side: string): string => (side === 'Debtor' || side === 'Debit' ? 'بدهکار' : side === 'Creditor' || side === 'Credit' ? 'بستانکار' : '—');

const AccountCardContent: React.FC = () => {
  const { values, updateFilters, setPage, companyId } = useReportFilters();
  const page = values.page ?? 1;

  const card = useQuery({
    queryKey: ['reports', 'account-card', companyId, values.accountId, values.fiscalYearId, values.fiscalPeriodId, values.from, values.to, values.detailId, values.costCenterId, values.projectId, values.partyId, values.currencyCode, page],
    queryFn: () => getAccountCard(values.accountId!, {
      fiscalYearId: values.fiscalYearId,
      fiscalPeriodId: values.fiscalPeriodId,
      from: values.from,
      to: values.to,
      detailId: values.detailId,
      costCenterId: values.costCenterId,
      projectId: values.projectId,
      partyId: values.partyId,
      currencyCode: values.currencyCode,
      page,
      pageSize: PAGE_SIZE,
    }),
    enabled: Boolean(values.accountId),
  });

  const rows = card.data?.rows ?? [];
  const totalCount = card.data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <div className="space-y-4" data-testid="account-card-page">
      <div>
        <h1 className="flex items-center gap-2 text-lg font-black text-foreground">
          <NotebookText className="size-5 text-primary" aria-hidden />
          کارت حساب
        </h1>
        <p className="mt-0.5 text-xs text-muted-foreground">گردش ردیفی با مانده تراکمی از backend — مانده‌ها با جهت واقعی نشان داده می‌شوند</p>
      </div>

      <ReportFilterBar
        values={values}
        onValuesChange={updateFilters}
        enabled={['account', 'year', 'period', 'dates', 'detail', 'costCenter', 'project', 'party', 'currency']}
        testIdPrefix="card"
      />

      {!values.accountId ? (
        <Card>
          <CardContent className="py-10 text-center text-sm text-muted-foreground">
            برای مشاهده کارت حساب، ابتدا یک حساب انتخاب کنید.
          </CardContent>
        </Card>
      ) : (
        <>
          {card.data && (
            <Card>
              <CardHeader className="flex flex-wrap items-center justify-between gap-3 border-b border-border/60 pb-3">
                <div className="text-sm">
                  <span dir="ltr" className="font-code font-bold">{card.data.accountCode}</span>
                  <span className="ms-2 font-bold">{card.data.accountName}</span>
                  <Badge variant="secondary" className="ms-2">{digitsToPersian(totalCount)} گردش</Badge>
                </div>
                <div className="flex flex-wrap items-center gap-4 text-xs">
                  <span>مانده اول دوره: <strong className="tabular-nums" data-testid="card-opening">{formatAmount(card.data.openingBalance)}</strong> <Badge variant="outline">{sideLabel(card.data.openingSide)}</Badge></span>
                  <span>مانده پایان دوره: <strong className="tabular-nums" data-testid="card-closing">{formatAmount(card.data.closingBalance)}</strong> <Badge variant="outline">{sideLabel(card.data.closingSide)}</Badge></span>
                </div>
              </CardHeader>
              <CardContent className="p-0">
                <FinancialTable
                  columns={[
                    { key: 'date', header: 'تاریخ', render: (row) => <span dir="ltr" className="tabular-nums">{formatToPersianDate(row.entryDate)}</span> },
                    { key: 'number', header: 'شماره سند', render: (row) => <DocNumberCell documentNumber={row.documentNumber} testId="card-doc" /> },
                    { key: 'desc', header: 'شرح', render: (row) => <DescriptionCell text={row.description} /> },
                    { key: 'debit', header: 'بدهکار', align: 'end', render: (row) => <MoneyCell value={row.debit} side="debit" /> },
                    { key: 'credit', header: 'بستانکار', align: 'end', render: (row) => <MoneyCell value={row.credit} side="credit" /> },
                    { key: 'running', header: 'مانده', align: 'end', render: (row) => <BalanceCell amount={row.runningBalance} direction={row.balanceSide === 'Debtor' ? 'Debtor' : row.balanceSide === 'Creditor' ? 'Creditor' : undefined} testId="card-running" /> },
                  ]}
                  rows={rows}
                  rowKey={(row, index) => `${row.documentNumber}-${index}`}
                  loading={card.isLoading}
                  emptyTitle="گردشی برای این حساب در بازه انتخابی ثبت نشده"
                  ariaLabel="کارت حساب"
                  footer={(
                    <TotalsFooter
                      colSpanBefore={5}
                      cells={[
                        <BalanceCell key="b" amount={card.data?.closingBalance ?? '0'} direction={card.data?.closingSide === 'Debtor' ? 'Debtor' : card.data?.closingSide === 'Creditor' ? 'Creditor' : undefined} testId="card-closing-footer" />,
                      ]}
                    />
                  )}
                />
              </CardContent>
            </Card>
          )}

          <div className="flex items-center justify-between gap-2">
            <span className="text-xs text-muted-foreground">صفحه {digitsToPersian(page)} از {digitsToPersian(totalPages)}</span>
            <div className="flex items-center gap-1">
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)} aria-label="صفحه قبل">
                <ChevronRight className="size-3.5" aria-hidden />
              </Button>
              <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)} aria-label="صفحه بعد">
                <ChevronLeft className="size-3.5" aria-hidden />
              </Button>
            </div>
          </div>
        </>
      )}
    </div>
  );
};

export const AccountCardPage: React.FC = () => (
  <PermissionGuard permissions={[PERMISSIONS.reportsRead]}>
    <AccountCardContent />
  </PermissionGuard>
);
