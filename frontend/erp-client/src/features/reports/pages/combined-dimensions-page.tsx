/**
 * گزارش ابعاد مالی ترکیبی — GET /accounting/dimensions/analysis/combined
 * انتخاب ۱ تا ۳ نوع بعد (باسط backend)؛ هشدار IsTruncated و سقف MaxRows.
 * dimensionTypes به‌صورت کلید تکرارشونده در query ارسال می‌شود (قرارداد binding).
 */
import * as React from 'react';
import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, Layers3 } from 'lucide-react';
import { Card, CardContent } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import { PermissionGuard } from '@/app/guards/permission-guard';
import { PERMISSIONS } from '@/core/auth/permissions';
import { getCombinedDimensionAnalysis } from '@/core/accounting/reports.service';
import { DIMENSION_TYPE } from '@/core/accounting/reports.types';
import type { FinancialDimensionTypeName, CombinedDimensionRowDto } from '@/core/accounting/reports.types';
import { useReportFilters } from '@/features/reports/use-report-filters';
import { ReportFilterBar } from '@/features/reports/components/report-filter-bar';
import { FinancialTable, MoneyCell, BalanceCell, TotalsFooter } from '@/features/reports/components/financial-table';

const TYPE_OPTIONS: Array<{ value: FinancialDimensionTypeName; label: string }> = [
  { value: 'DetailAccount', label: 'تفصیلی شناور' },
  { value: 'CostCenter', label: 'مرکز هزینه' },
  { value: 'Project', label: 'پروژه' },
  { value: 'Party', label: 'شخص' },
  { value: 'Branch', label: 'شعبه' },
  { value: 'Contract', label: 'قرارداد' },
  { value: 'Customer', label: 'مشتری' },
];

const typeLabel = (value: string): string => TYPE_OPTIONS.find((option) => option.value === value)?.label ?? value;

const CombinedContent: React.FC = () => {
  const { values, updateFilters, companyId } = useReportFilters();
  const [selectedTypes, setSelectedTypes] = React.useState<FinancialDimensionTypeName[]>(['CostCenter', 'Project']);

  const toggleType = (type: FinancialDimensionTypeName) => {
    setSelectedTypes((prev) => {
      if (prev.includes(type)) return prev.filter((item) => item !== type);
      if (prev.length >= 3) return prev; // سقف ۳ بعد طبق قرارداد backend
      return [...prev, type];
    });
  };

  const report = useQuery({
    queryKey: ['reports', 'combined-dimensions', companyId, selectedTypes.join(','), values.fiscalYearId, values.from, values.to],
    queryFn: () => getCombinedDimensionAnalysis({
      dimensionTypes: selectedTypes.map((type) => DIMENSION_TYPE[type]),
      fiscalYearId: values.fiscalYearId,
      fromDate: values.from,
      toDate: values.to,
      maxRows: 500,
    }),
    enabled: selectedTypes.length > 0,
  });

  return (
    <div className="space-y-4" data-testid="combined-dimensions-page">
      <div>
        <h1 className="flex items-center gap-2 text-lg font-black text-foreground">
          <Layers3 className="size-5 text-primary" aria-hidden />
          گزارش ابعاد مالی ترکیبی
        </h1>
        <p className="mt-0.5 text-xs text-muted-foreground">ترکیب ۱ تا ۳ نوع بعد — ارسال dimensionTypes تکرارشونده مطابق binding واقعی backend</p>
      </div>

      <div className="flex flex-wrap items-center gap-2 rounded-lg border border-border/60 bg-surface px-3 py-2.5">
        <span className="text-xs font-bold">نوع ابعاد (حداکثر ۳):</span>
        {TYPE_OPTIONS.map((option) => (
          <button
            key={option.value}
            type="button"
            className={`rounded-md border px-2.5 py-1.5 text-xs font-semibold transition-colors ${selectedTypes.includes(option.value) ? 'border-primary bg-primary/10 text-primary' : 'border-border text-muted-foreground hover:bg-muted'}`}
            onClick={() => toggleType(option.value)}
            aria-pressed={selectedTypes.includes(option.value)}
            data-testid={`combined-type-${option.value}`}
          >
            {option.label}
          </button>
        ))}
      </div>

      <ReportFilterBar values={values} onValuesChange={updateFilters} enabled={['year', 'dates']} testIdPrefix="combined" />

      {report.data?.isTruncated && (
        <p className="flex items-center gap-1.5 rounded-md border border-warning/40 bg-warning/5 px-3 py-2 text-xs text-warning" data-testid="combined-truncated">
          <AlertTriangle className="size-3.5" aria-hidden />
          تعداد ردیف‌های منطبق {report.data.totalMatchedRows} بود و خروجی به سقف backend بریده شد (IsTruncated).
        </p>
      )}

      <Card>
        <CardContent className="p-0">
          <FinancialTable<CombinedDimensionRowDto>
            columns={[
              ...[0, 1, 2].map((slot) => ({
                key: `dim-${slot}`,
                header: slot === 0 ? 'بعد اول' : slot === 1 ? 'بعد دوم' : 'بعد سوم',
                render: (row: CombinedDimensionRowDto) => {
                  const ref = row.dimensions[slot];
                  if (!ref) return <span className="text-muted-foreground">—</span>;
                  return (
                    <span className="whitespace-nowrap">
                      <span dir="ltr" className="font-code text-[11px] text-muted-foreground">{ref.code}</span>
                      <span className="ms-1.5">{ref.name}</span>
                      <Badge variant="outline" className="ms-1 text-[9px]">{typeLabel(ref.dimensionType)}</Badge>
                    </span>
                  );
                },
              })),
              { key: 'debit', header: 'گردش بدهکار', align: 'end' as const, render: (row: CombinedDimensionRowDto) => <MoneyCell value={row.debit} side="debit" /> },
              { key: 'credit', header: 'گردش بستانکار', align: 'end' as const, render: (row: CombinedDimensionRowDto) => <MoneyCell value={row.credit} side="credit" /> },
              { key: 'balance', header: 'مانده', align: 'end' as const, render: (row: CombinedDimensionRowDto) => <BalanceCell amount={row.displayBalance} direction={row.direction} /> },
            ]}
            rows={report.data?.rows}
            rowKey={(row) => row.dimensions.map((ref) => `${ref.dimensionType}:${ref.dimensionId}`).join('|')}
            loading={report.isLoading}
            emptyTitle="ترکیب انتخابی نتیجه‌ای ندارد"
            ariaLabel="گزارش ابعاد ترکیبی"
            footer={(
              <TotalsFooter
                colSpanBefore={3}
                cells={[
                  <MoneyCell key="d" value={report.data?.totalDebit ?? '0'} side="debit" testId="combined-total-debit" />,
                  <MoneyCell key="c" value={report.data?.totalCredit ?? '0'} side="credit" testId="combined-total-credit" />,
                  <span key="b" />,
                ]}
              />
            )}
          />
        </CardContent>
      </Card>
    </div>
  );
};

export const CombinedDimensionsPage: React.FC = () => (
  <PermissionGuard permissions={[PERMISSIONS.dimensionsRead]}>
    <CombinedContent />
  </PermissionGuard>
);
