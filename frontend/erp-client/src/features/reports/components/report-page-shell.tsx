/**
 * پوسته مشترک گزارش‌گیری (فاز ۸):
 * عنوان/توضیح، نشان شرکت فعال (تعویض شرکت از هدر جهانی انجام می‌شود)، فیلترهای مالی
 * وابسته، دکمه «اجرای گزارش»، زمان تولید، خلاصه، جزئیات، جمع‌ها، حالت بدون داده،
 * مدیریت خطا + خروجی‌های سمت سرور (Excel/PDF/CSV مطابق فیلترها) و چاپ با قالب RTL.
 * query پس از اولین «اجرا» فعال می‌شود (enabledUntilRun) تا گزارش با فیلترهای
 * انتخابی کاربر اجرا شود، نه خودکار با پیش‌فرض‌ها.
 */
import * as React from 'react';
import { Building2, FileSpreadsheet, FileText, Printer, Play, RefreshCcw, Clock } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { ErrorState } from '@/shared/components/feedback/error-state';
import { ReportFilterBar, type ReportFilterKind } from '@/features/reports/components/report-filter-bar';
import type { ReportUrlFilters } from '@/features/reports/use-report-filters';
import { useCompanyStore } from '@/core/company/company.store';
import { downloadReportExport, saveBlob } from '@/core/accounting/statements.service';
import type { ExportableReportName, ReportExportFormatName } from '@/core/accounting/statements.types';
import { toast } from '@/core/notifications/toast';

export interface ReportPageShellProps {
  title: string;
  description: string;
  icon: React.ReactNode;
  /** فیلترهای مالی از URL صفحه */
  filters: ReportUrlFilters;
  /** انواع فیلتر فعال این گزارش */
  filterKinds: ReportFilterKind[];
  /** اجرای گزارش — فعال‌سازی query صفحه */
  onRun: () => void;
  loading?: boolean;
  error?: boolean;
  onRetry?: () => void;
  /** زمان آخرین تولید موفق (epoch ms از dataUpdatedAt) */
  generatedAt?: number;
  /** خلاصه بالای جدول (chipها/کارت‌های خلاصه) */
  summary?: React.ReactNode;
  /** جدول اصلی + جزئیات + جمع‌های نهایی */
  children: React.ReactNode;
  /** نام گزارش خروجی‌پذیر در backend — بدون آن فقط چاپ */
  exportReportName?: ExportableReportName;
  /** پارامترهای فیلتر برای endpoint خروجی — منطبق با فیلترهای انتخابی */
  exportParams?: Record<string, string | undefined>;
  testIdPrefix?: string;
}

const EXPORT_FORMATS: Array<{ format: ReportExportFormatName; label: string; icon: React.ReactNode }> = [
  { format: 'Excel', label: 'اکسل', icon: <FileSpreadsheet className="size-3.5" aria-hidden /> },
  { format: 'Pdf', label: 'PDF', icon: <FileText className="size-3.5" aria-hidden /> },
  { format: 'Csv', label: 'CSV', icon: <FileText className="size-3.5" aria-hidden /> },
];

const formatClock = (epoch: number): string => {
  try {
    return new Intl.DateTimeFormat('fa-IR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(epoch));
  } catch {
    return new Date(epoch).toLocaleString('fa-IR');
  }
};

export const ReportPageShell: React.FC<ReportPageShellProps> = ({
  title, description, icon, filters, filterKinds, onRun, loading, error, onRetry, generatedAt, summary, children, exportReportName, exportParams, testIdPrefix = 'report',
}) => {
  const activeCompany = useCompanyStore((state) => state.activeCompany);
  const [exporting, setExporting] = React.useState<ReportExportFormatName | null>(null);

  const runExport = async (format: ReportExportFormatName) => {
    if (!exportReportName) return;
    setExporting(format);
    try {
      const { fileName, blob } = await downloadReportExport({ reportName: exportReportName, format, ...exportParams });
      saveBlob(blob, fileName);
      toast.success('فایل خروجی آماده شد', `${fileName} — محاسبات سمت سرور، منطبق با فیلترهای فعلی`);
    } catch {
      toast.error('تولید فایل ناموفق بود', 'خروجی این گزارش با فیلترهای فعلی از سرور دریافت نشد');
    } finally {
      setExporting(null);
    }
  };

  return (
    <div className="space-y-4" data-testid={`${testIdPrefix}-page`}>
      {/* سربرگ — در چاپ به‌صورت عنوان گزارش درج می‌شود */}
      <header className="report-print-header flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-lg font-black text-foreground">
            {icon}
            {title}
          </h1>
          <p className="mt-0.5 max-w-2xl text-xs leading-5 text-muted-foreground">{description}</p>
        </div>
        {activeCompany && (
          <span className="flex items-center gap-1.5 rounded-md border border-border/60 bg-surface px-2.5 py-1.5 text-[11px] font-semibold text-muted-foreground" title="تعویض شرکت از هدر بالای صفحه">
            <Building2 className="size-3.5" aria-hidden />
            {activeCompany.name}
          </span>
        )}
      </header>

      {/* نوار کنترل: فیلترها + اجرا + خروجی + چاپ */}
      <div className="report-no-print space-y-2">
        <ReportFilterBar values={filters.values} onValuesChange={filters.updateFilters} enabled={filterKinds} testIdPrefix={testIdPrefix} />
        <div className="flex flex-wrap items-center gap-2">
          <Button size="sm" onClick={onRun} disabled={loading} data-testid={`${testIdPrefix}-run`}>
            {loading ? <RefreshCcw className="size-3.5 animate-spin" aria-hidden /> : <Play className="size-3.5" aria-hidden />}
            اجرای گزارش
          </Button>
          {exportReportName && EXPORT_FORMATS.map(({ format, label, icon }) => (
            <Button key={format} variant="outline" size="sm" disabled={exporting !== null || loading} onClick={() => void runExport(format)} data-testid={`${testIdPrefix}-export-${format.toLowerCase()}`}>
              {icon}
              {exporting === format ? 'در حال تولید…' : label}
            </Button>
          ))}
          <Button variant="outline" size="sm" onClick={() => window.print()} data-testid={`${testIdPrefix}-print`}>
            <Printer className="size-3.5" aria-hidden />
            چاپ
          </Button>
          {generatedAt ? (
            <span className="ms-auto flex items-center gap-1 text-[11px] text-muted-foreground" data-testid={`${testIdPrefix}-generated-at`}>
              <Clock className="size-3" aria-hidden />
              تولید گزارش: {formatClock(generatedAt)} — محاسبات سمت سرور
            </span>
          ) : (
            <span className="ms-auto text-[11px] text-muted-foreground">گزارش هنوز اجرا نشده است</span>
          )}
        </div>
      </div>

      {error ? (
        <ErrorState title="اجرای گزارش ناموفق بود" description="دریافت داده از سرور با خطا مواجه شد؛ فیلترها را بررسی و دوباره اجرا کنید." onRetry={onRetry} data-testid={`${testIdPrefix}-error`} />
      ) : (
        <>
          {loading && !generatedAt ? (
            <div className="space-y-2" aria-busy="true" role="status" data-testid={`${testIdPrefix}-loading`}>
              <Skeleton className="h-16 w-full" />
              <Skeleton className="h-64 w-full" />
            </div>
          ) : (
            <>
              {generatedAt !== undefined && summary && <section className="report-no-print" data-testid={`${testIdPrefix}-summary`}>{summary}</section>}
              <section>{children}</section>
            </>
          )}
        </>
      )}
    </div>
  );
};
