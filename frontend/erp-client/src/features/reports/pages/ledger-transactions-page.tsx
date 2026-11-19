/**
 * گردش تفصیلی حساب در دفتر کل — GET /accounting/ledger/general-ledger/accounts/{id}/transactions
 * مانده اول، گردش بدهکار/بستانکار، مانده پایان + ردیف‌ها با لینک سند.
 */
import * as React from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ArrowRight, ChevronLeft, ChevronRight, ListOrdered } from 'lucide-react';
import { Card, CardContent, CardHeader } from '@/shared/components/ui/card';
import { Button } from '@/shared/components/ui/button';
import { ErrorState } from '@/shared/components/feedback/error-state';
import { PermissionGuard } from '@/app/guards/permission-guard';
import { PERMISSIONS } from '@/core/auth/permissions';
import { getLedgerTransactions } from '@/core/accounting/reports.service';
import { FinancialTable, MoneyCell, BalanceCell, TotalsFooter, DocNumberCell, DescriptionCell } from '@/features/reports/components/financial-table';
import { formatToPersianDate } from '@/shared/utils/date';
import { digitsToPersian } from '@/shared/utils/number';

const PAGE_SIZE = 25;

const TransactionsContent: React.FC = () => {
  const { accountId } = useParams<{ accountId: string }>();
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const page = Number(params.get('page') ?? '1') || 1;

  const setPage = (nextPage: number) => {
    const next = new URLSearchParams(params);
    next.set('page', String(Math.max(1, nextPage)));
    setParams(next, { replace: true });
  };

  const report = useQuery({
    queryKey: ['reports', 'ledger-transactions', accountId, params.get('fiscalYearId'), params.get('from'), params.get('to'), page],
    queryFn: () => getLedgerTransactions(accountId!, {
      fiscalYearId: params.get('fiscalYearId') || undefined,
      fromDate: params.get('from') || undefined,
      toDate: params.get('to') || undefined,
      pageNumber: page,
      pageSize: PAGE_SIZE,
    }),
  });

  if (report.isError) {
    return <ErrorState title="بارگذاری گردش حساب ناموفق بود" onRetry={() => void report.refetch()} />;
  }

  const rows = report.data?.transactions ?? [];
  const totalCount = report.data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <div className="space-y-4" data-testid="ledger-transactions-page">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="icon" onClick={() => navigate('/accounting/reports/general-ledger')} aria-label="بازگشت به دفتر کل">
            <ArrowRight className="size-4" aria-hidden />
          </Button>
          <div>
            <h1 className="flex items-center gap-2 text-lg font-black text-foreground">
              <ListOrdered className="size-5 text-primary" aria-hidden />
              گردش تفصیلی — {report.data ? `${report.data.accountFullCode} ${report.data.accountName}` : 'حساب'}
            </h1>
            <p className="mt-0.5 text-xs text-muted-foreground">سطر به سطر از موتور مانده backend</p>
          </div>
        </div>
        {report.data && (
          <div className="flex flex-wrap items-center gap-3 rounded-lg border border-border/60 bg-surface px-3 py-2 text-xs">
            <span>مانده اول: <strong className="tabular-nums">{report.data.openingBalance}</strong></span>
            <span className="text-sky-700 dark:text-sky-400">جمع بدهکار: <strong className="tabular-nums">{report.data.totalDebit}</strong></span>
            <span className="text-amber-700 dark:text-amber-400">جمع بستانکار: <strong className="tabular-nums">{report.data.totalCredit}</strong></span>
            <span>مانده پایان: <strong className="tabular-nums">{report.data.closingDisplayBalance}</strong></span>
          </div>
        )}
      </div>

      <Card>
        <CardHeader className="border-b border-border/60 pb-2.5 text-sm font-bold">گردش‌ها</CardHeader>
        <CardContent className="p-0">
          <FinancialTable
            columns={[
              { key: 'date', header: 'تاریخ', render: (row) => <span dir="ltr" className="tabular-nums">{formatToPersianDate(row.entryDate)}</span> },
              { key: 'number', header: 'شماره سند', render: (row) => <DocNumberCell documentNumber={row.documentNumber} documentId={row.documentId} testId="lt-doc" /> },
              { key: 'desc', header: 'شرح', render: (row) => <DescriptionCell text={row.description} /> },
              { key: 'line', header: 'ردیف', align: 'center', render: (row) => digitsToPersian(row.lineNumber) },
              { key: 'debit', header: 'بدهکار', align: 'end', render: (row) => <MoneyCell value={row.debit} side="debit" /> },
              { key: 'credit', header: 'بستانکار', align: 'end', render: (row) => <MoneyCell value={row.credit} side="credit" /> },
              { key: 'currency', header: 'ارز', render: (row) => row.currencyCode },
            ]}
            rows={rows}
            rowKey={(row) => row.entryId}
            loading={report.isLoading}
            emptyTitle="گردشی برای این حساب در بازه انتخابی نیست"
            ariaLabel="گردش تفصیلی حساب"
            footer={(
              <TotalsFooter
                colSpanBefore={5}
                cells={[
                  <BalanceCell key="b" amount={report.data?.closingDisplayBalance ?? '0'} direction={report.data?.closingDirection} testId="lt-closing" />,
                  <span key="cur" />,
                ]}
              />
            )}
          />
        </CardContent>
      </Card>

      <div className="flex items-center justify-between gap-2">
        <span className="text-xs text-muted-foreground">{digitsToPersian(totalCount)} گردش · صفحه {digitsToPersian(page)} از {digitsToPersian(totalPages)}</span>
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

export const LedgerTransactionsPage: React.FC = () => (
  <PermissionGuard permissions={[PERMISSIONS.ledgerRead]}>
    <TransactionsContent />
  </PermissionGuard>
);
