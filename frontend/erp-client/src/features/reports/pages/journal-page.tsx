/**
 * دفتر روزنامه — GET /accounting/ledger/journal (صفحه‌بندی سمت سرور)
 * Entries با ردیف‌های آن، جمع روزانه و جمع دوره؛ لینک و کپی شماره سند.
 */
import * as React from 'react';
import { useQuery } from '@tanstack/react-query';
import { BookOpenText } from 'lucide-react';
import { Card, CardContent } from '@/shared/components/ui/card';
import { Button } from '@/shared/components/ui/button';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { PermissionGuard } from '@/app/guards/permission-guard';
import { PERMISSIONS } from '@/core/auth/permissions';
import { getLedgerJournal } from '@/core/accounting/reports.service';
import { ReportFilterBar } from '@/features/reports/components/report-filter-bar';
import { useReportFilters } from '@/features/reports/use-report-filters';
import { FinancialTable, MoneyCell, DescriptionCell, TotalsFooter, DocNumberCell } from '@/features/reports/components/financial-table';
import { formatAmount } from '@/core/accounting/documents.service';
import { formatToPersianDate } from '@/shared/utils/date';
import { digitsToPersian } from '@/shared/utils/number';
import type { JournalEntryDto } from '@/core/accounting/reports.types';

const PAGE_SIZE = 25;

const JournalContent: React.FC = () => {
  const { values, updateFilters, setPage, companyId } = useReportFilters();
  const page = values.page ?? 1;

  const report = useQuery({
    queryKey: ['reports', 'journal', companyId, values.fiscalYearId, values.from, values.to, values.accountId, values.currencyCode, page],
    queryFn: () => getLedgerJournal({
      fiscalYearId: values.fiscalYearId,
      fromDate: values.from,
      toDate: values.to,
      accountId: values.accountId,
      pageNumber: page,
      pageSize: PAGE_SIZE,
    }),
  });

  const entries = report.data?.entries ?? [];
  const totalCount = report.data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <div className="space-y-4" data-testid="journal-page">
      <div>
        <h1 className="flex items-center gap-2 text-lg font-black text-foreground">
          <BookOpenText className="size-5 text-primary" aria-hidden />
          دفتر روزنامه
        </h1>
        <p className="mt-0.5 text-xs text-muted-foreground">اسناد قطعی‌شده به ترتیب تاریخ — صفحه‌بندی سمت سرور (server-side pagination)</p>
      </div>

      <ReportFilterBar
        values={values}
        onValuesChange={updateFilters}
        enabled={['year', 'period', 'dates', 'account']}
        testIdPrefix="journal"
      />

      <Card>
        <CardContent className="p-0">
          <FinancialTable<JournalEntryDto>
            columns={[
              { key: 'number', header: 'شماره سند', render: (row) => <DocNumberCell documentNumber={row.documentNumber} documentId={row.documentId} testId="journal-doc" /> },
              { key: 'date', header: 'تاریخ', render: (row) => <span dir="ltr" className="tabular-nums">{formatToPersianDate(row.documentDate)}</span> },
              { key: 'desc', header: 'شرح سند', render: (row) => <DescriptionCell text={row.description} /> },
              { key: 'lines', header: 'ردیف‌ها', render: (row) => (
                <span className="text-[11px] text-muted-foreground" title={row.lines.map((line) => `${line.accountFullCode} ${line.accountName}: ${formatAmount(line.debit)} / ${formatAmount(line.credit)}`).join('\n')}>
                  {digitsToPersian(row.lines.length)} ردیف
                </span>
              ) },
              { key: 'debit', header: 'بدهکار', align: 'end', render: (row) => <MoneyCell value={row.totalDebit} side="debit" /> },
              { key: 'credit', header: 'بستانکار', align: 'end', render: (row) => <MoneyCell value={row.totalCredit} side="credit" /> },
            ]}
            rows={entries}
            rowKey={(row) => row.documentId}
            loading={report.isLoading}
            ariaLabel="دفتر روزنامه"
            footer={(
              <TotalsFooter
                colSpanBefore={4}
                cells={[
                  <MoneyCell key="d" value={report.data?.periodTotalDebit ?? '0'} side="debit" testId="journal-period-debit" />,
                  <MoneyCell key="c" value={report.data?.periodTotalCredit ?? '0'} side="credit" testId="journal-period-credit" />,
                ]}
              />
            )}
          />
        </CardContent>
      </Card>

      <div className="flex items-center justify-between gap-2">
        <span className="text-xs text-muted-foreground">{digitsToPersian(totalCount)} سند · صفحه {digitsToPersian(page)} از {digitsToPersian(totalPages)}</span>
        <div className="flex items-center gap-1">
          <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)} aria-label="صفحه قبل" data-testid="journal-prev-page">
            <ChevronRight className="size-3.5" aria-hidden />
          </Button>
          <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)} aria-label="صفحه بعد" data-testid="journal-next-page">
            <ChevronLeft className="size-3.5" aria-hidden />
          </Button>
        </div>
      </div>
    </div>
  );
};

export const JournalPage: React.FC = () => (
  <PermissionGuard permissions={[PERMISSIONS.ledgerRead]}>
    <JournalContent />
  </PermissionGuard>
);
