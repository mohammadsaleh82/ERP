/**
 * لیست اسناد حسابداری — فیلترهای واقعی SearchDocumentsRequest (وضعیت/بازه تاریخ/شماره سند/سال)
 * همه فیلترها در querystring نگهداری می‌شوند (قابل اشتراک‌گذاری و بازگشت)، صفحه‌بندی و مرتب‌سازی
 * سمت server (sortBy)؛ نمایش جزئیات با Drawer.
 */
import * as React from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ChevronLeft, ChevronRight, Eye, FileText, Plus, Printer, RefreshCw, X } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Card, CardContent } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import { Input } from '@/shared/components/ui/input';
import { Drawer } from '@/shared/components/ui/drawer';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { EmptyState } from '@/shared/components/feedback/empty-state';
import { ErrorState } from '@/shared/components/feedback/error-state';
import { useAuthStore } from '@/core/auth/auth.store';
import { PERMISSIONS } from '@/core/auth/permissions';
import { searchDocuments, getDocument } from '@/core/accounting/documents.service';
import { statusLabel, statusVariant, typeLabel } from '@/features/documents/document-labels';
import { formatAmount, parseAmountInput } from '@/core/accounting/documents.service';
import { formatToPersianDate } from '@/shared/utils/date';
import { digitsToPersian } from '@/shared/utils/number';
import { cn } from '@/shared/utils/cn';
import type { DocumentDetailDto, DocumentDto, DocumentStatus } from '@/core/accounting/documents.types';

const STATUS_OPTIONS: Array<{ value: DocumentStatus; label: string }> = [
  { value: 'Draft', label: 'پیش‌نویس' },
  { value: 'PendingApproval', label: 'در انتظار تأیید' },
  { value: 'Reviewed', label: 'بررسی‌شده' },
  { value: 'Approved', label: 'تاییدشده' },
  { value: 'Posted', label: 'ثبت قطعی' },
  { value: 'Reversed', label: 'باطل‌شده' },
];

export const VouchersPage: React.FC = () => {
  const navigate = useNavigate();
  const hasAny = useAuthStore((state) => state.hasAnyPermission);
  const canCreate = hasAny([PERMISSIONS.documentsCreate]);

  const [params, setParams] = useSearchParams();

  // فیلترها در URL — برگشت‌پذیر و قابل اشتراک‌گذاری
  const status = params.get('status') ?? '';
  const fromDate = params.get('from') ?? '';
  const toDate = params.get('to') ?? '';
  const documentNumberRaw = params.get('number') ?? '';
  const page = Number(params.get('page') ?? '1');
  const sortBy = params.get('sortBy') ?? 'DocumentDate';
  const sortDir = (params.get('sortDir') ?? 'desc') as 'asc' | 'desc';
  const pageSize = 20;

  const documentNumber = documentNumberRaw ? Number(parseAmountInput(documentNumberRaw)) : undefined;

  /** به‌روزرسانی یکجا چند پارامتر — از stale closure جلوگیری می‌کند */
  const updateParams = (updates: Record<string, string | undefined>) => {
    const next = new URLSearchParams(params);
    for (const [key, value] of Object.entries(updates)) {
      if (value) next.set(key, value);
      else next.delete(key);
      if (key !== 'page') next.delete('page');
    }
    setParams(next, { replace: true });
  };
  const setParam = (key: string, value: string) => updateParams({ [key]: value });

  const documents = useQuery({
    queryKey: ['documents', 'list', { status, fromDate, toDate, documentNumber, page, sortBy, sortDir }],
    queryFn: () => searchDocuments({
      status: status || undefined,
      fromDate: fromDate || undefined,
      toDate: toDate || undefined,
      documentNumber: documentNumber || undefined,
      pageNumber: page,
      pageSize,
      sortBy: sortBy ? `${sortBy}:${sortDir}` : undefined,
    }),
    staleTime: 1000 * 30,
  });

  const toggleSort = (column: string) => {
    if (sortBy === column) updateParams({ sortDir: sortDir === 'asc' ? 'desc' : 'asc' });
    else updateParams({ sortBy: column, sortDir: 'desc' });
  };

  const [detailId, setDetailId] = React.useState<string | null>(null);
  const detail = useQuery({
    queryKey: ['documents', 'detail', detailId],
    queryFn: () => getDocument(detailId!),
    enabled: Boolean(detailId),
  });

  const total = documents.data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  return (
    <div className="space-y-4" data-testid="vouchers-page">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-lg font-black text-foreground">
            <FileText className="size-5 text-primary" aria-hidden />
            اسناد حسابداری
          </h1>
          <p className="mt-0.5 text-xs text-muted-foreground">فیلترها در نشانی صفحه نگهداری می‌شوند — پیوند صفحه قابل اشتراک‌گذاری است</p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="ghost" size="icon" onClick={() => void documents.refetch()} aria-label="بارگذاری مجدد">
            <RefreshCw className="size-4" aria-hidden />
          </Button>
          {canCreate && (
            <Button size="sm" onClick={() => navigate('/accounting/documents/new')} data-testid="create-document">
              <Plus className="size-3.5" aria-hidden />
              سند جدید
            </Button>
          )}
        </div>
      </div>

      {/* نوار فیلتر */}
      <div className="flex flex-wrap items-center gap-2 rounded-lg border border-border/60 bg-surface px-3 py-2.5" data-testid="vouchers-filterbar">
        <label className="flex items-center gap-1.5 text-xs font-semibold" htmlFor="filter-status">وضعیت:</label>
        <select id="filter-status" value={status} onChange={(event) => setParam('status', event.target.value)} className="h-9 rounded-md border border-border bg-surface px-2 text-xs" data-testid="filter-status">
          <option value="">همه</option>
          {STATUS_OPTIONS.map((option) => (
            <option key={option.value} value={option.value}>{option.label}</option>
          ))}
        </select>
        <label htmlFor="filter-from" className="sr-only">از تاریخ</label>
        <Input id="filter-from" type="date" dir="ltr" value={fromDate} onChange={(event) => setParam('from', event.target.value)} className="h-9 w-36 text-xs" data-testid="filter-from" />
        <label htmlFor="filter-to" className="sr-only">تا تاریخ</label>
        <Input id="filter-to" type="date" dir="ltr" value={toDate} onChange={(event) => setParam('to', event.target.value)} className="h-9 w-36 text-xs" data-testid="filter-to" />
        <Input
          value={documentNumberRaw}
          onChange={(event) => setParam('number', event.target.value)}
          placeholder="شماره سند"
          className="h-9 w-28 text-xs"
          inputMode="numeric"
          data-testid="filter-number"
        />
        {(status || fromDate || toDate || documentNumberRaw) && (
          <Button variant="ghost" size="sm" onClick={() => setParams({}, { replace: true })} data-testid="clear-filters">
            <X className="size-3.5" aria-hidden />
            پاک کردن فیلترها
          </Button>
        )}
      </div>

      {/* جدول */}
      <Card>
        <CardContent className="p-0">
          {documents.isLoading ? (
            <div className="space-y-2 p-4" aria-busy="true" role="status">
              {Array.from({ length: 5 }).map((_, index) => <Skeleton key={index} className="h-9 w-full" />)}
            </div>
          ) : documents.isError ? (
            <div className="p-4"><ErrorState title="بارگذاری اسناد ناموفق بود" onRetry={() => void documents.refetch()} /></div>
          ) : (documents.data?.items ?? []).length === 0 ? (
            <EmptyState title="سندی یافت نشد" description="فیلترها را تغییر دهید یا اولین سند را ثبت کنید." />
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-xs" data-testid="documents-table">
                <thead className="bg-muted/40 text-muted-foreground">
                  <tr>
                    <th className="px-2.5 py-2 text-start font-semibold">
                      <button type="button" className="inline-flex items-center gap-1" onClick={() => toggleSort('DocumentNumber')} aria-label="مرتب‌سازی بر اساس شماره سند">
                        شماره {sortBy === 'DocumentNumber' && <SortArrow dir={sortDir} />}
                      </button>
                    </th>
                    <th className="px-2.5 py-2 text-start font-semibold">
                      <button type="button" className="inline-flex items-center gap-1" onClick={() => toggleSort('DocumentDate')} aria-label="مرتب‌سازی بر اساس تاریخ">
                        تاریخ {sortBy === 'DocumentDate' && <SortArrow dir={sortDir} />}
                      </button>
                    </th>
                    <th className="px-2.5 py-2 text-start font-semibold">شرح</th>
                    <th className="px-2.5 py-2 text-start font-semibold">نوع</th>
                    <th className="px-2.5 py-2 text-start font-semibold">وضعیت</th>
                    <th className="px-2.5 py-2 text-end font-semibold">جمع</th>
                    <th className="px-2.5 py-2 text-end font-semibold">عملیات</th>
                  </tr>
                </thead>
                <tbody>
                  {(documents.data?.items ?? []).map((document: DocumentDto) => (
                    <tr key={document.id} className="border-t border-border/40 hover:bg-muted/30">
                      <td className="px-2.5 py-2 tabular-nums">{digitsToPersian(document.documentNumber)}</td>
                      <td dir="ltr" className="px-2.5 py-2 text-start tabular-nums">{formatToPersianDate(document.documentDate)}</td>
                      <td className="max-w-56 truncate px-2.5 py-2">{document.description}</td>
                      <td className="px-2.5 py-2">{typeLabel(document.documentType)}</td>
                      <td className="px-2.5 py-2"><Badge variant={statusVariant(document.status)}>{statusLabel(document.status)}</Badge></td>
                      <td dir="ltr" className="px-2.5 py-2 text-end tabular-nums">{formatAmount(document.totalDebit)}</td>
                      <td className="px-2.5 py-2 text-end">
                        <Button variant="ghost" size="sm" className="h-7 text-xs" onClick={() => navigate(`/accounting/documents/${document.id}`)} data-testid={`open-doc-${document.documentNumber}`}>
                          <Eye className="size-3" aria-hidden />
                          مشاهده
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>

      {/* صفحه‌بندی */}
      <div className="flex items-center justify-between gap-2">
        <span className="text-xs text-muted-foreground">
          {digitsToPersian(total)} سند · صفحه {digitsToPersian(page)} از {digitsToPersian(totalPages)}
        </span>
        <div className="flex items-center gap-1">
          <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setParam('page', String(page - 1))} aria-label="صفحه قبل" data-testid="prev-page">
            <ChevronRight className="size-3.5" aria-hidden />
          </Button>
          <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setParam('page', String(page + 1))} aria-label="صفحه بعد" data-testid="next-page">
            <ChevronLeft className="size-3.5" aria-hidden />
          </Button>
        </div>
      </div>

      {/* جزئیات سریع */}
      <Drawer open={Boolean(detailId)} onOpenChange={(open) => !open && setDetailId(null)} title="خلاصه سند" widthClass="max-w-lg">
        {detail.isLoading && <Skeleton className="h-40 w-full" />}
        {detail.data && (
          <QuickSummary document={detail.data} onPrint={() => window.print()} onOpen={() => detail.data && navigate(`/accounting/documents/${detail.data.id}`)} />
        )}
      </Drawer>
    </div>
  );
};

const SortArrow: React.FC<{ dir: 'asc' | 'desc' }> = ({ dir }) => (
  <span aria-hidden className={cn('text-[9px]', dir === 'asc' ? 'rotate-180' : '')}>▲</span>
);

const QuickSummary: React.FC<{ document: DocumentDetailDto; onPrint: () => void; onOpen: () => void }> = ({ document, onPrint, onOpen }) => (
  <div className="space-y-3" data-testid="quick-summary">
    <div className="flex items-center justify-between">
      <span className="text-sm font-bold">سند {digitsToPersian(document.documentNumber)}</span>
      <Badge variant={statusVariant(document.status)}>{statusLabel(document.status)}</Badge>
    </div>
    <p className="text-xs text-muted-foreground">{document.description}</p>
    <div className="flex items-center justify-between">
      <Button size="sm" onClick={onOpen}>مشاهده کامل</Button>
      <Button size="sm" variant="outline" onClick={onPrint}><Printer className="size-3.5" aria-hidden />چاپ</Button>
    </div>
  </div>
);
