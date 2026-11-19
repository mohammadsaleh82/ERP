/**
 * گردش ابعاد مالی — GET /accounting/dimensions/analysis/turnover
 * نوع بعد از URL (detail/party/costCenter/project)؛ انتخاب بعد مشخص اختیاری؛
 * فیلتر سال/بازه؛ پرسوناهای واقعی از DimensionsController (بدون بازسازی محلی Entity).
 */
import * as React from 'react';
import { useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ChevronLeft, ChevronRight, Shapes } from 'lucide-react';
import { Card, CardContent } from '@/shared/components/ui/card';
import { Button } from '@/shared/components/ui/button';
import { PermissionGuard } from '@/app/guards/permission-guard';
import { PERMISSIONS } from '@/core/auth/permissions';
import { getDimensionTurnover, getFinancialDimensions } from '@/core/accounting/reports.service';
import { DIMENSION_TYPE } from '@/core/accounting/reports.types';
import type { FinancialDimensionTypeName, DimensionTurnoverRowDto } from '@/core/accounting/reports.types';
import { useReportFilters } from '@/features/reports/use-report-filters';
import { ReportFilterBar } from '@/features/reports/components/report-filter-bar';
import { FinancialTable, MoneyCell, BalanceCell, TotalsFooter, DescriptionCell } from '@/features/reports/components/financial-table';
import { digitsToPersian } from '@/shared/utils/number';

const TABS: Array<{ slug: string; type: FinancialDimensionTypeName; title: string; note: string }> = [
  { slug: 'detail', type: 'DetailAccount', title: 'گردش تفصیلی', note: 'تفصیلی شناور (معین‌شناور) از ماژول ابعاد مالی backend' },
  { slug: 'parties', type: 'Party', title: 'گردش اشخاص', note: 'اشخاص متعلق به ماژول Parties — نمایش از طریق قرارداد ابعاد backend' },
  { slug: 'cost-centers', type: 'CostCenter', title: 'گردش مراکز هزینه', note: 'مراکز هزینه واقعی ابعاد مالی' },
  { slug: 'projects', type: 'Project', title: 'گردش پروژه‌ها', note: 'پروژه‌های واقعی ابعاد مالی' },
];

const PAGE_SIZE = 25;

const DimensionTurnoverContent: React.FC = () => {
  const [params, setParams] = useSearchParams();
  const { values, updateFilters, companyId } = useReportFilters();
  const tabSlug = params.get('tab') ?? 'detail';
  const activeTab = TABS.find((tab) => tab.slug === tabSlug) ?? TABS[0];
  const dimensionId = params.get('dimensionId') ?? '';

  const dimensions = useQuery({
    queryKey: ['dimensions', activeTab.type, companyId, 'list'],
    queryFn: () => getFinancialDimensions({ dimensionType: DIMENSION_TYPE[activeTab.type], pageSize: 200, isActive: true }),
    staleTime: 1000 * 120,
  });

  const report = useQuery({
    queryKey: ['reports', 'dimension-turnover', companyId, activeTab.type, dimensionId, values.fiscalYearId, values.from, values.to, values.page],
    queryFn: () => getDimensionTurnover({
      dimensionType: DIMENSION_TYPE[activeTab.type],
      dimensionId: dimensionId || undefined,
      fiscalYearId: values.fiscalYearId,
      fromDate: values.from,
      toDate: values.to,
      pageNumber: values.page,
      pageSize: PAGE_SIZE,
    }),
  });

  const setTab = (slug: string) => {
    const next = new URLSearchParams(params);
    next.set('tab', slug);
    next.delete('dimensionId');
    next.delete('page');
    setParams(next, { replace: true });
  };

  const selectDimension = (id: string) => {
    const next = new URLSearchParams(params);
    if (id) next.set('dimensionId', id);
    else next.delete('dimensionId');
    next.delete('page');
    setParams(next, { replace: true });
  };

  const selectDimensionPage = (nextPage: number) => {
    const next = new URLSearchParams(params);
    next.set('page', String(Math.max(1, nextPage)));
    setParams(next, { replace: true });
  };

  const rows = report.data?.rows ?? [];
  const totalCount = report.data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));
  const page = values.page ?? 1;

  return (
    <div className="space-y-4" data-testid="dimensions-turnover-page">
      <div>
        <h1 className="flex items-center gap-2 text-lg font-black text-foreground">
          <Shapes className="size-5 text-primary" aria-hidden />
          گردش ابعاد مالی
        </h1>
        <p className="mt-0.5 text-xs text-muted-foreground">{activeTab.note}</p>
      </div>

      {/* تب نوع بعد */}
      <div className="flex flex-wrap gap-1 rounded-lg border border-border/60 bg-surface p-1" role="tablist" data-testid="dimension-tabs">
        {TABS.map((tab) => (
          <button
            key={tab.slug}
            role="tab"
            aria-selected={tab.slug === tabSlug}
            className={`rounded-md px-3 py-1.5 text-xs font-semibold ${tab.slug === tabSlug ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:bg-muted'}`}
            onClick={() => setTab(tab.slug)}
            data-testid={`tab-${tab.slug}`}
          >
            {tab.title}
          </button>
        ))}
      </div>

      {/* انتخاب بعد مشخص + فیلترهای مالی */}
      <div className="flex flex-wrap items-end gap-2 rounded-lg border border-border/60 bg-surface px-3 py-2.5">
        <label className="flex flex-col gap-1 text-[11px] font-semibold" htmlFor="dimension-select">
          {activeTab.title.replace('گردش ', '')}
          <select id="dimension-select" value={dimensionId} onChange={(event) => selectDimension(event.target.value)} className="h-9 w-56 rounded-md border border-border bg-surface px-2 text-xs" data-testid="dimension-select">
            <option value="">همه ابعاد این نوع</option>
            {(dimensions.data?.items ?? []).map((dimension) => (
              <option key={dimension.id} value={dimension.id}>{dimension.code} — {dimension.name}</option>
            ))}
          </select>
        </label>
        <ReportFilterBar values={values} onValuesChange={updateFilters} enabled={['year', 'dates']} testIdPrefix="dim" />
      </div>

      <Card>
        <CardContent className="p-0">
          <FinancialTable<DimensionTurnoverRowDto>
            columns={[
              { key: 'code', header: 'کد', render: (row) => <span dir="ltr" className="font-code text-[11px]">{row.dimensionCode || '—'}</span> },
              { key: 'name', header: 'عنوان', render: (row) => <DescriptionCell text={row.dimensionName} /> },
              { key: 'debit', header: 'گردش بدهکار', align: 'end', render: (row) => <MoneyCell value={row.debit} side="debit" /> },
              { key: 'credit', header: 'گردش بستانکار', align: 'end', render: (row) => <MoneyCell value={row.credit} side="credit" /> },
              { key: 'balance', header: 'مانده', align: 'end', render: (row) => <BalanceCell amount={row.displayBalance} direction={row.direction} testId="dim-balance" /> },
            ]}
            rows={rows}
            rowKey={(row) => row.dimensionId ?? row.dimensionCode}
            loading={report.isLoading}
            emptyTitle="گردشی برای این بعد یافت نشد"
            ariaLabel={activeTab.title}
            footer={(
              <TotalsFooter
                colSpanBefore={2}
                cells={[
                  <MoneyCell key="d" value={report.data?.totalDebit ?? '0'} side="debit" testId="dim-total-debit" />,
                  <MoneyCell key="c" value={report.data?.totalCredit ?? '0'} side="credit" testId="dim-total-credit" />,
                  <span key="b" />,
                ]}
              />
            )}
          />
        </CardContent>
      </Card>

      <div className="flex items-center justify-between gap-2">
        <span className="text-xs text-muted-foreground">{digitsToPersian(totalCount)} ردیف · صفحه {digitsToPersian(page)} از {digitsToPersian(totalPages)}</span>
        <div className="flex items-center gap-1">
          <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => selectDimensionPage(page - 1)} aria-label="صفحه قبل" data-testid="dim-prev-page">
            <ChevronRight className="size-3.5" aria-hidden />
          </Button>
          <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => selectDimensionPage(page + 1)} aria-label="صفحه بعد" data-testid="dim-next-page">
            <ChevronLeft className="size-3.5" aria-hidden />
          </Button>
        </div>
      </div>
    </div>
  );
};

export const DimensionsTurnoverPage: React.FC = () => (
  <PermissionGuard permissions={[PERMISSIONS.dimensionsRead]}>
    <DimensionTurnoverContent />
  </PermissionGuard>
);
