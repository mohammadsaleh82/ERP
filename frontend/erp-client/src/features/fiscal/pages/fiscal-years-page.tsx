/**
 * سال‌های مالی و دوره‌ها — endpointهای واقعی:
 * GET /FiscalYears · POST create/open/close · GET periods · POST periods/generate ·
 * POST periods/custom · POST periods/{id}/open|close
 * تاریخ‌ها: مرز مالی ISO DateOnly از API؛ نمایش شمسی صرفاً display و تاریخ مالی را تغییر نمی‌دهد.
 */
import * as React from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { CalendarRange, Lock, LockOpen, Plus, RefreshCw, Wand2 } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Card, CardContent } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import { Modal } from '@/shared/components/ui/modal';
import { Drawer } from '@/shared/components/ui/drawer';
import { ConfirmDialog } from '@/shared/components/ui/confirm-dialog';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { EmptyState } from '@/shared/components/feedback/empty-state';
import { ErrorState } from '@/shared/components/feedback/error-state';
import { useAuthStore } from '@/core/auth/auth.store';
import { PERMISSIONS } from '@/core/auth/permissions';
import * as fiscalApi from '@/core/accounting/fiscal.service';
import { ApiError } from '@/core/errors/problem-details';
import { calendarLabel, generationLabel, periodStatusLabel, yearStatusLabel } from '@/features/fiscal/fiscal-labels';
import { FiscalYearCreateDialog } from '@/features/fiscal/components/fiscal-year-create-dialog';
import { CustomPeriodDialog } from '@/features/fiscal/components/custom-period-dialog';
import { toast } from '@/core/notifications/toast';
import { formatToPersianDate } from '@/shared/utils/date';
import { digitsToPersian } from '@/shared/utils/number';
import type { FiscalPeriodDto, FiscalYearDto } from '@/core/accounting/fiscal.types';

const yearStatusVariant = (status: FiscalYearDto['status']) =>
  status === 'Open' ? 'success' : status === 'Closed' ? 'destructive' : status === 'Closing' ? 'warning' : 'secondary';

const periodStatusVariant = (status: FiscalPeriodDto['status']) =>
  status === 'Open' ? 'success' : status === 'Closed' ? 'destructive' : status === 'Locked' ? 'warning' : 'secondary';

export const FiscalYearsPage: React.FC = () => {
  const queryClient = useQueryClient();
  const hasAny = useAuthStore((state) => state.hasAnyPermission);
  const canCreate = hasAny([PERMISSIONS.fiscalYearsCreate]);
  const canUpdate = hasAny([PERMISSIONS.fiscalYearsUpdate]);
  const canClosePeriod = hasAny([PERMISSIONS.periodClose]);

  const [detailYear, setDetailYear] = React.useState<FiscalYearDto | null>(null);
  const [createOpen, setCreateOpen] = React.useState(false);
  const [confirmYearClose, setConfirmYearClose] = React.useState<FiscalYearDto | null>(null);
  const [confirmYearOpen, setConfirmYearOpen] = React.useState<FiscalYearDto | null>(null);

  const years = useQuery({
    queryKey: ['fiscal-years', 'list'],
    queryFn: fiscalApi.getAllFiscalYears,
    staleTime: 1000 * 60,
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ['fiscal-years'] });
    void queryClient.invalidateQueries({ queryKey: ['dashboard'] });
  };

  const openMutation = useMutation({
    mutationFn: fiscalApi.openFiscalYear,
    onSuccess: () => {
      toast.success('سال مالی باز شد', 'دوره‌های آن برای ثبت اسناد آماده است.');
      invalidate();
      setConfirmYearOpen(null);
    },
    onError: (error) => {
      toast.error('باز کردن سال مالی انجام نشد', error instanceof ApiError ? error.message : undefined);
      setConfirmYearOpen(null);
    },
  });

  const closeMutation = useMutation({
    mutationFn: fiscalApi.closeFiscalYear,
    onSuccess: () => {
      toast.success('سال مالی بسته شد', 'انتقال مانده‌های پایانی انجام و سال از گردش جاری خارج شد.');
      invalidate();
      setConfirmYearClose(null);
    },
    onError: (error) => {
      toast.error('بستن سال مالی انجام نشد', error instanceof ApiError ? error.message : 'پیش‌نیازهای اختتامیه کامل نیست.');
      setConfirmYearClose(null);
    },
  });

  return (
    <div className="space-y-4" data-testid="fiscal-years-page">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-lg font-black text-foreground">
            <CalendarRange className="size-5 text-primary" aria-hidden />
            سال‌های مالی و دوره‌ها
          </h1>
          <p className="mt-0.5 text-xs text-muted-foreground">
            مرزهای تاریخ مالی مطابق API؛ نمایش شمسی صرفاً برای خوانایی است و تاریخ مالی را تغییر نمی‌دهد
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={() => void years.refetch()} aria-label="بارگذاری مجدد">
            <RefreshCw className="size-3.5" aria-hidden />
          </Button>
          {canCreate && (
            <Button size="sm" onClick={() => setCreateOpen(true)} data-testid="create-fiscal-year">
              <Plus className="size-3.5" aria-hidden />
              سال مالی جدید
            </Button>
          )}
        </div>
      </div>

      {years.isLoading ? (
        <div className="space-y-3" aria-busy="true" role="status">
          <Skeleton className="h-24 w-full" />
          <Skeleton className="h-24 w-full" />
        </div>
      ) : years.isError ? (
        <ErrorState title="بارگذاری سال‌های مالی ناموفق بود" onRetry={() => void years.refetch()} />
      ) : (years.data ?? []).length === 0 ? (
        <EmptyState
          title="سال مالی ثبت نشده است"
          description="برای شروع ثبت اسناد، ابتدا یک سال مالی با تقویم شمسی ایجاد کنید."
          actionText={canCreate ? 'ایجاد سال مالی' : undefined}
          onAction={canCreate ? () => setCreateOpen(true) : undefined}
        />
      ) : (
        <div className="space-y-3">
          {(years.data ?? []).map((year) => (
            <Card key={year.id} className="overflow-hidden" data-testid={`fiscal-year-${year.yearNumber}`}>
              <CardContent className="flex flex-wrap items-center justify-between gap-3 p-4">
                <div className="min-w-0">
                  <p className="flex items-center gap-2 text-sm font-bold text-foreground">
                    سال مالی {digitsToPersian(year.yearNumber)} — {year.name}
                    <Badge variant={yearStatusVariant(year.status)}>{yearStatusLabel(year.status)}</Badge>
                    {year.isClosed && <Badge variant="destructive" className="text-[10px]">بسته</Badge>}
                  </p>
                  <p className="mt-1 text-[11px] text-muted-foreground">
                    <span dir="ltr" className="tabular-nums">{formatToPersianDate(year.startDate)} تا {formatToPersianDate(year.endDate)}</span>
                    · تقویم {calendarLabel(year.calendarType)}
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <Button variant="outline" size="sm" onClick={() => setDetailYear(year)} data-testid={`detail-year-${year.yearNumber}`}>
                    دوره‌ها و جزئیات
                  </Button>
                  {canUpdate && year.status === 'Draft' && (
                    <Button variant="outline" size="sm" onClick={() => setConfirmYearOpen(year)} data-testid={`open-year-${year.yearNumber}`}>
                      <LockOpen className="size-3.5" aria-hidden />
                      باز کردن سال
                    </Button>
                  )}
                  {canUpdate && (year.status === 'Open' || year.status === 'Closing') && (
                    <Button variant="outline" size="sm" className="text-warning" onClick={() => setConfirmYearClose(year)} data-testid={`close-year-${year.yearNumber}`}>
                      <Lock className="size-3.5" aria-hidden />
                      بستن سال
                    </Button>
                  )}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <FiscalYearCreateDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        onSaved={(year) => {
          invalidate();
          setDetailYear(year);
        }}
      />

      {/* جزئیات + دوره‌ها */}
      <FiscalYearDetail
        year={detailYear}
        open={Boolean(detailYear)}
        onOpenChange={(open) => !open && setDetailYear(null)}
        canUpdate={canUpdate}
        canClosePeriod={canClosePeriod}
        onChanged={invalidate}
      />

      <ConfirmDialog
        open={Boolean(confirmYearOpen)}
        onOpenChange={(open) => !open && setConfirmYearOpen(null)}
        title={`باز کردن سال مالی ${confirmYearOpen?.yearNumber ?? ''}`}
        description="سال در وضعیت «باز» قرار می‌گیرد و دوره‌های باز آن پذیرای اسناد جدید خواهند بود."
        actionLabel="باز کن"
        onConfirm={() => confirmYearOpen && openMutation.mutate(confirmYearOpen.id)}
      />
      <ConfirmDialog
        open={Boolean(confirmYearClose)}
        onOpenChange={(open) => !open && setConfirmYearClose(null)}
        title={`بستن سال مالی ${confirmYearClose?.yearNumber ?? ''}`}
        description={
          <span>
            عملیات قطعی است: اختتامیه اجرا و مانده‌های پایانی به سال بعد منتقل می‌شود. سال بسته‌شده دیگر پذیرای سند نیست
            و بازگشت آن به «باز» نیازمند مجوز بازگشایی است.
          </span>
        }
        actionLabel="بستن قطعی سال"
        onConfirm={() => confirmYearClose && closeMutation.mutate(confirmYearClose.id)}
      />
    </div>
  );
};

interface DetailProps {
  year: FiscalYearDto | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  canUpdate: boolean;
  canClosePeriod: boolean;
  onChanged: () => void;
}

/** جزئیات سال + جدول دوره‌ها با عملیات open/close/generate/custom */
export const FiscalYearDetail: React.FC<DetailProps> = ({ year, open, onOpenChange, canUpdate, canClosePeriod, onChanged }) => {
  const [generateOpen, setGenerateOpen] = React.useState(false);
  const [customOpen, setCustomOpen] = React.useState(false);
  const [confirmPeriod, setConfirmPeriod] = React.useState<{ period: FiscalPeriodDto; action: 'open' | 'close' } | null>(null);

  const periods = useQuery({
    queryKey: ['fiscal-periods', year?.id],
    queryFn: () => fiscalApi.getFiscalPeriods(year!.id),
    enabled: Boolean(year?.id) && open,
    staleTime: 1000 * 30,
  });

  const openPeriodMutation = useMutation({
    mutationFn: (periodId: string) => fiscalApi.openPeriod(year!.id, periodId),
    onSuccess: () => {
      toast.success('دوره باز شد', 'دوره برای ثبت اسناد جدید آماده است.');
      void periods.refetch();
      onChanged();
      setConfirmPeriod(null);
    },
    onError: (error) => {
      toast.error('باز کردن دوره انجام نشد', error instanceof ApiError ? error.message : undefined);
      setConfirmPeriod(null);
    },
  });

  const closePeriodMutation = useMutation({
    mutationFn: (periodId: string) => fiscalApi.closePeriod(year!.id, periodId),
    onSuccess: () => {
      toast.success('دوره بسته شد', 'دوره بسته دیگر پذیرای سند نیست.');
      void periods.refetch();
      onChanged();
      setConfirmPeriod(null);
    },
    onError: (error) => {
      toast.error('بستن دوره انجام نشد', error instanceof ApiError ? error.message : 'اسناد باز این دوره ممکن است عملیات را متوقف کند.');
      setConfirmPeriod(null);
    },
  });

  return (
    <Drawer open={open} onOpenChange={onOpenChange} title={year ? `دوره‌های سال مالی ${year.name}` : ''} widthClass="max-w-2xl">
      {year && (
        <div className="space-y-4" data-testid="fiscal-year-detail">
          <dl className="grid grid-cols-2 gap-2 rounded-md border border-border/60 p-3 text-xs sm:grid-cols-4">
            <div><dt className="text-muted-foreground">شماره سال</dt><dd className="font-bold">{digitsToPersian(year.yearNumber)}</dd></div>
            <div><dt className="text-muted-foreground">وضعیت</dt><dd><Badge variant={yearStatusVariant(year.status)}>{yearStatusLabel(year.status)}</Badge></dd></div>
            <div><dt className="text-muted-foreground">شروع</dt><dd dir="ltr" className="tabular-nums">{formatToPersianDate(year.startDate)}</dd></div>
            <div><dt className="text-muted-foreground">پایان</dt><dd dir="ltr" className="tabular-nums">{formatToPersianDate(year.endDate)}</dd></div>
          </dl>

          <div className="flex items-center justify-between gap-2">
            <p className="text-sm font-bold text-foreground">دوره‌های مالی</p>
            {canUpdate && (
              <div className="flex items-center gap-2">
                {(periods.data ?? []).length === 0 && (
                  <Button variant="outline" size="sm" onClick={() => setGenerateOpen(true)} data-testid="generate-periods">
                    <Wand2 className="size-3.5" aria-hidden />
                    تولید خودکار دوره‌ها
                  </Button>
                )}
                <Button variant="outline" size="sm" onClick={() => setCustomOpen(true)} data-testid="add-custom-period">
                  <Plus className="size-3.5" aria-hidden />
                  دوره سفارشی
                </Button>
              </div>
            )}
          </div>

          {periods.isLoading ? (
            <Skeleton className="h-32 w-full" />
          ) : periods.isError ? (
            <ErrorState title="بارگذاری دوره‌ها ناموفق بود" onRetry={() => void periods.refetch()} />
          ) : (periods.data ?? []).length === 0 ? (
            <EmptyState title="دوره‌ای تعریف نشده" description="دوره‌های ماهانه را به‌صورت خودکار تولید کنید یا دوره سفارشی بسازید." />
          ) : (
            <div className="overflow-x-auto rounded-md border border-border/60">
              <table className="w-full text-xs" data-testid="periods-table">
                <thead className="bg-muted/40 text-muted-foreground">
                  <tr>
                    <th className="px-2.5 py-2 text-start font-semibold">#</th>
                    <th className="px-2.5 py-2 text-start font-semibold">دوره</th>
                    <th className="px-2.5 py-2 text-start font-semibold">بازه</th>
                    <th className="px-2.5 py-2 text-start font-semibold">وضعیت</th>
                    <th className="px-2.5 py-2 text-end font-semibold">عملیات</th>
                  </tr>
                </thead>
                <tbody>
                  {(periods.data ?? []).map((period) => (
                    <tr key={period.id} className="border-t border-border/40">
                      <td className="px-2.5 py-2 tabular-nums">{digitsToPersian(period.periodNumber)}</td>
                      <td className="px-2.5 py-2 font-medium">
                        {period.name}
                        {period.isAdjustmentPeriod && <Badge variant="warning" className="ms-1 text-[9px]">تعدیلات</Badge>}
                      </td>
                      <td dir="ltr" className="px-2.5 py-2 text-start tabular-nums text-muted-foreground">
                        {formatToPersianDate(period.startDate)} — {formatToPersianDate(period.endDate)}
                      </td>
                      <td className="px-2.5 py-2"><Badge variant={periodStatusVariant(period.status)}>{periodStatusLabel(period.status)}</Badge></td>
                      <td className="px-2.5 py-2 text-end">
                        {canUpdate && period.status !== 'Closed' && period.status !== 'Open' && (
                          <Button variant="ghost" size="sm" className="h-7 text-xs" onClick={() => setConfirmPeriod({ period, action: 'open' })}>
                            <LockOpen className="size-3" aria-hidden />
                            باز
                          </Button>
                        )}
                        {canClosePeriod && period.status === 'Open' && (
                          <Button variant="ghost" size="sm" className="h-7 text-xs text-warning" onClick={() => setConfirmPeriod({ period, action: 'close' })} data-testid={`close-period-${period.periodNumber}`}>
                            <Lock className="size-3" aria-hidden />
                            بستن
                          </Button>
                        )}
                        {(period.status === 'Closed' || period.status === 'Open') && !canClosePeriod && <span className="text-[10px] text-muted-foreground">—</span>}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {/* تولید خودکار */}
      <GeneratePeriodsDialog
        open={generateOpen}
        onOpenChange={setGenerateOpen}
        year={year}
        onDone={() => {
          void periods.refetch();
          onChanged();
        }}
      />

      {/* دوره سفارشی */}
      <CustomPeriodDialog
        open={customOpen}
        onOpenChange={setCustomOpen}
        year={year}
        onDone={() => {
          void periods.refetch();
          onChanged();
        }}
      />

      <ConfirmDialog
        open={Boolean(confirmPeriod)}
        onOpenChange={(open) => !open && setConfirmPeriod(null)}
        title={confirmPeriod?.action === 'close' ? `بستن دوره «${confirmPeriod?.period.name}»` : `باز کردن دوره «${confirmPeriod?.period.name}»`}
        description={
          confirmPeriod?.action === 'close'
            ? 'پس از بستن، این دوره دیگر پذیرای سند جدید نیست. بازگشایی دوره بسته عملیات عادی نیست.'
            : 'دوره در وضعیت «باز» قرار می‌گیرد و اسناد بازه آن قابل ثبت می‌شوند.'
        }
        actionLabel={confirmPeriod?.action === 'close' ? 'بستن دوره' : 'باز کردن'}
        onConfirm={() => {
          if (!confirmPeriod) return;
          if (confirmPeriod.action === 'close') closePeriodMutation.mutate(confirmPeriod.period.id);
          else openPeriodMutation.mutate(confirmPeriod.period.id);
        }}
      />
    </Drawer>
  );
};

/** تولید خودکار دوره‌ها — GeneratePeriodsRequest واقعی */
const GeneratePeriodsDialog: React.FC<{ open: boolean; onOpenChange: (open: boolean) => void; year: FiscalYearDto | null; onDone: () => void }> = ({
  open,
  onOpenChange,
  year,
  onDone,
}) => {
  const [type, setType] = React.useState<'Monthly' | 'Quarterly'>('Monthly');
  const mutation = useMutation({
    mutationFn: () => fiscalApi.generatePeriods(year!.id, type),
    onSuccess: () => {
      toast.success('دوره‌ها تولید شدند', generationLabel(type));
      onDone();
      onOpenChange(false);
    },
    onError: (error) => {
      toast.error('تولید دوره‌ها انجام نشد', error instanceof ApiError ? error.message : undefined);
    },
  });

  return (
    <Modal open={open} onOpenChange={onOpenChange} title="تولید خودکار دوره‌های مالی" size="sm">
      <div className="space-y-3">
        <label className="block text-xs font-semibold text-foreground" htmlFor="generation-type">نوع تولید</label>
        <select
          id="generation-type"
          value={type}
          onChange={(event) => setType(event.target.value as 'Monthly' | 'Quarterly')}
          className="h-9 w-full rounded-md border border-border bg-surface px-2 text-sm"
          data-testid="generation-type"
        >
          <option value="Monthly">{generationLabel('Monthly')}</option>
          <option value="Quarterly">{generationLabel('Quarterly')}</option>
        </select>
        <p className="text-[11px] text-muted-foreground">مرزهای تاریخ هر دوره را backend بر اساس تقویم مالی سال محاسبه می‌کند.</p>
        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={() => onOpenChange(false)}>انصراف</Button>
          <Button onClick={() => mutation.mutate()} disabled={mutation.isPending} data-testid="generate-confirm">
            {mutation.isPending ? 'در حال تولید…' : 'تولید'}
          </Button>
        </div>
      </div>
    </Modal>
  );
};
