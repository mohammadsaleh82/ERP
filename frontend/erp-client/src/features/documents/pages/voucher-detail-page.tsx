/**
 * مشاهده سند — read-only مطابق وضعیت واقعی backend:
 * سند ثبت‌شده (Posted) هرگز به شکل قابل ویرایش نمایش داده نمی‌شود؛ فقط پیش‌نویس
 * با مجوز update به فرم ویرایش می‌رود. شامل عملیات گردش کار، تاریخچه تغییرات
 * (audit-trail) و چاپ A4.
 */
import * as React from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ArrowRight, History, Pencil, Printer } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Card, CardContent, CardHeader } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { Drawer } from '@/shared/components/ui/drawer';
import { ErrorState } from '@/shared/components/feedback/error-state';
import { useAuthStore } from '@/core/auth/auth.store';
import { PERMISSIONS } from '@/core/auth/permissions';
import { getDocument, getDocumentAuditTrail, formatAmount } from '@/core/accounting/documents.service';
import { statusAllowsEdit, statusLabel, statusVariant, typeLabel } from '@/features/documents/document-labels';
import { VoucherWorkflow } from '@/features/documents/components/voucher-workflow';
import { VoucherForm } from '@/features/documents/components/voucher-form';
import { VoucherPrint } from '@/features/documents/components/voucher-print';
import { formatToPersianDate, formatToPersianDateTime } from '@/shared/utils/date';
import { digitsToPersian } from '@/shared/utils/number';
import { useCompanyStore } from '@/core/company/company.store';

export const VoucherDetailPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const hasAny = useAuthStore((state) => state.hasAnyPermission);
  const companyName = useCompanyStore((state) => state.activeCompany?.name ?? 'شرکت');

  const [mode, setMode] = React.useState<'view' | 'edit'>('view');
  const [historyOpen, setHistoryOpen] = React.useState(false);

  const document = useQuery({
    queryKey: ['documents', 'detail', id],
    queryFn: () => getDocument(id!),
    enabled: Boolean(id),
  });

  const audit = useQuery({
    queryKey: ['documents', 'audit', id],
    queryFn: () => getDocumentAuditTrail(id!),
    enabled: Boolean(id) && historyOpen,
  });

  if (document.isLoading) {
    return (
      <div className="space-y-3" aria-busy="true" role="status">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (document.isError || !document.data) {
    return <ErrorState title="بارگذاری سند ناموفق بود" description="ممکن است سند حذف شده یا مجوز مشاهده آن را نداشته باشید." onRetry={() => void document.refetch()} />;
  }

  const data = document.data;
  const isEditable = statusAllowsEdit(data.status) && hasAny([PERMISSIONS.documentsUpdate]);

  return (
    <div className="space-y-4" data-testid="voucher-detail-page">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="icon" onClick={() => navigate('/accounting/documents')} aria-label="بازگشت به لیست">
            <ArrowRight className="size-4" aria-hidden />
          </Button>
          <div>
            <h1 className="text-lg font-black text-foreground">
              سند {digitsToPersian(data.documentNumber)}
              <Badge variant={statusVariant(data.status)} className="ms-2 align-middle">{statusLabel(data.status)}</Badge>
              {data.status === 'Reversed' && data.reversedDocumentId && (
                <Badge variant="outline" className="ms-1 align-middle">سند اصلی باطل شده</Badge>
              )}
            </h1>
            <p className="mt-0.5 text-xs text-muted-foreground">
              {formatToPersianDate(data.documentDate)} · {typeLabel(data.documentType)} · ارز {data.currencyCode}
              {data.referenceNumber && ` · مرجع ${data.referenceNumber}`}
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={() => setHistoryOpen(true)} data-testid="show-history">
            <History className="size-3.5" aria-hidden />تاریخچه
          </Button>
          <Button variant="outline" size="sm" onClick={() => window.print()} data-testid="print-button">
            <Printer className="size-3.5" aria-hidden />چاپ
          </Button>
          {isEditable && mode === 'view' && (
            <Button size="sm" onClick={() => setMode('edit')} data-testid="edit-draft">
              <Pencil className="size-3.5" aria-hidden />ویرایش پیش‌نویس
            </Button>
          )}
        </div>
      </div>

      {/* سند ثبت‌شده هرگز قابل ویرایش نمایش داده نمی‌شود */}
      {data.status === 'Posted' && (
        <p className="rounded-md border border-success/40 bg-success/5 px-3 py-2 text-xs text-success" data-testid="posted-readonly-note">
          این سند ثبت قطعی شده است و به شکل قابل ویرایش نمایش داده نمی‌شود — اصلاح فقط با سند معکوس یا تعدیل.
        </p>
      )}
      {data.status === 'Reversed' && (
        <p className="rounded-md border border-destructive/40 bg-destructive/5 px-3 py-2 text-xs" data-testid="reversed-note">
          این سند با سند معکوس باطل شده است.
        </p>
      )}
      {data.status === 'Draft' && data.rejectionReason && (
        <p className="rounded-md border border-warning/40 bg-warning/5 px-3 py-2 text-xs" data-testid="rejection-note">
          دلیل رد قبلی: {data.rejectionReason}
        </p>
      )}

      {mode === 'edit' && isEditable ? (
        <VoucherForm
          editingDocument={data}
          onSaved={() => setMode('view')}
        />
      ) : (
        <>
          <Card>
            <CardHeader className="border-b border-border/60 pb-3">
              <h2 className="text-sm font-bold">{data.description}</h2>
            </CardHeader>
            <CardContent className="p-0">
              <div className="overflow-x-auto">
                <table className="w-full text-xs" data-testid="document-lines">
                  <thead className="bg-muted/40 text-muted-foreground">
                    <tr>
                      <th className="px-2.5 py-2 text-start font-semibold">ردیف</th>
                      <th className="px-2.5 py-2 text-start font-semibold">کد حساب</th>
                      <th className="px-2.5 py-2 text-start font-semibold">نام حساب</th>
                      <th className="px-2.5 py-2 text-start font-semibold">شرح</th>
                      <th className="px-2.5 py-2 text-end font-semibold">بدهکار</th>
                      <th className="px-2.5 py-2 text-end font-semibold">بستانکار</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.lines.map((line) => (
                      <tr key={line.id} className="border-t border-border/40">
                        <td className="px-2.5 py-2 tabular-nums">{digitsToPersian(line.lineNumber)}</td>
                        <td dir="ltr" className="px-2.5 py-2 text-start font-code">{line.accountFullCode}</td>
                        <td className="px-2.5 py-2">{line.accountName}</td>
                        <td className="px-2.5 py-2">{line.description}</td>
                        <td dir="ltr" className="px-2.5 py-2 text-end tabular-nums">{Number(line.debitAmount) !== 0 ? formatAmount(line.debitAmount) : '—'}</td>
                        <td dir="ltr" className="px-2.5 py-2 text-end tabular-nums">{Number(line.creditAmount) !== 0 ? formatAmount(line.creditAmount) : '—'}</td>
                      </tr>
                    ))}
                    <tr className="border-t-2 border-border bg-muted/30 font-bold">
                      <td colSpan={4} className="px-2.5 py-2">جمع کل</td>
                      <td dir="ltr" className="px-2.5 py-2 text-end tabular-nums" data-testid="view-total-debit">{formatAmount(data.totalDebit)}</td>
                      <td dir="ltr" className="px-2.5 py-2 text-end tabular-nums" data-testid="view-total-credit">{formatAmount(data.totalCredit)}</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </CardContent>
          </Card>

          <div className="flex flex-wrap items-center justify-between gap-3">
            <span className="text-[11px] text-muted-foreground">
              ایجاد: {data.createdBy} · {formatToPersianDateTime(data.createdAtUtc)}
              {data.postedAtUtc && ` · ثبت قطعی: ${formatToPersianDateTime(data.postedAtUtc)}`}
            </span>
            <VoucherWorkflow document={data} onChanged={() => void document.refetch()} onDelete={() => navigate('/accounting/documents')} />
          </div>
        </>
      )}

      {/* تاریخچه تغییرات */}
      <Drawer open={historyOpen} onOpenChange={setHistoryOpen} title="تاریخچه تغییرات سند" widthClass="max-w-md">
        {audit.isLoading && <Skeleton className="h-40 w-full" />}
        {audit.data && audit.data.length > 0 && (
          <ol className="space-y-3" data-testid="audit-trail">
            {audit.data.map((entry) => (
              <li key={entry.id} className="rounded-md border border-border/60 p-3 text-xs">
                <p className="font-bold">{entry.action}</p>
                {entry.previousStatus && entry.newStatus && (
                  <p className="mt-0.5 text-muted-foreground">
                    {statusLabel(entry.previousStatus)} → {statusLabel(entry.newStatus)}
                  </p>
                )}
                {entry.details && <p className="mt-1 text-[11px] text-muted-foreground">{entry.details}</p>}
                <p className="mt-1 text-[10px] text-muted-foreground">{entry.performedBy} · {formatToPersianDateTime(entry.performedAtUtc)}</p>
              </li>
            ))}
          </ol>
        )}
        {audit.data && audit.data.length === 0 && <p className="py-4 text-center text-xs text-muted-foreground">رخدادی ثبت نشده است</p>}
      </Drawer>

      {/* قالب چاپ */}
      <VoucherPrint document={data} companyName={companyName} />
    </div>
  );
};
