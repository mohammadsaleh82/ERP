/**
 * تنظیمات حسابداری شرکت — فقط فیلدهای واقعی AccountingSettingsDto/UpdateAccountingSettingsRequest:
 * ارز پایه، دقت اعشار، گرد کردن، تقویم، شماره‌گذاری، سیاست تأیید، سیاست مانده منفی،
 * LockDate، AllowUnbalancedDraft، RequireSeparateApprover/Poster، ChangeReason + نسخه (concurrency)
 * و تاریخچه نسخه‌ها (GET /settings/history).
 */
import * as React from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Settings2, History, RefreshCw } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Card, CardContent, CardHeader } from '@/shared/components/ui/card';
import { Input } from '@/shared/components/ui/input';
import { Textarea } from '@/shared/components/ui/textarea';
import { Badge } from '@/shared/components/ui/badge';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { Modal } from '@/shared/components/ui/modal';
import { ErrorState } from '@/shared/components/feedback/error-state';
import { Switch } from '@/shared/components/ui/selection-controls';
import { useAuthStore } from '@/core/auth/auth.store';
import { PERMISSIONS } from '@/core/auth/permissions';
import { getAccountingSettings, getSettingsHistory, updateAccountingSettings } from '@/core/accounting/settings.service';
import { ApiError } from '@/core/errors/problem-details';
import { toast } from '@/core/notifications/toast';
import { formatToPersianDateTime } from '@/shared/utils/date';
import { digitsToPersian } from '@/shared/utils/number';
import type { AccountingSettingsDto, AccountingSettingsHistoryDto, UpdateAccountingSettingsRequest } from '@/core/accounting/settings.types';

const roundingOptions = [
  { value: 'RoundToEven', label: 'نزدیک‌ترین زوج (بانکی)' },
  { value: 'AwayFromZero', label: 'دور از صفر (۰٫۵ به بالا)' },
  { value: 'Floor', label: 'کف (رو به پایین)' },
  { value: 'Ceiling', label: 'سقف (رو به بالا)' },
];

const numberingOptions = [
  { value: 'ContinuousByFiscalYear', label: 'متوالی در کل سال مالی' },
  { value: 'MonthlyRestart', label: 'شروع مجدد در هر دوره/ماه' },
  { value: 'DailyRestart', label: 'شروع مجدد در هر روز' },
];

const negativeOptions = [
  { value: 'StrictlyForbidden', label: 'ممنوع قطعی' },
  { value: 'WarningOnly', label: 'فقط هشدار' },
  { value: 'Allowed', label: 'مجاز' },
];

const approvalOptions = [
  { value: 'DirectPostingAllowed', label: 'ثبت مستقیم بدون تأیید' },
  { value: 'TwoStepApprovalRequired', label: 'دو مرحله‌ای (پیش‌نویس → تصویب)' },
  { value: 'ThreeStepApprovalRequired', label: 'سه مرحله‌ای (بررسی → تأیید)' },
];

export const AccountingSettingsPage: React.FC = () => {
  const queryClient = useQueryClient();
  const hasAny = useAuthStore((state) => state.hasAnyPermission);
  const canRead = hasAny([PERMISSIONS.settingsRead]);
  const canUpdate = hasAny([PERMISSIONS.settingsUpdate]);

  const [form, setForm] = React.useState<AccountingSettingsDto | null>(null);
  const [changeReason, setChangeReason] = React.useState('');
  const [historyOpen, setHistoryOpen] = React.useState(false);

  const settings = useQuery({
    queryKey: ['accounting-settings'],
    queryFn: getAccountingSettings,
    enabled: canRead,
    staleTime: 1000 * 30,
  });

  const history = useQuery({
    queryKey: ['accounting-settings-history'],
    queryFn: getSettingsHistory,
    enabled: historyOpen && canRead,
  });

  React.useEffect(() => {
    if (settings.data) setForm(settings.data);
  }, [settings.data]);

  const patch = (partial: Partial<AccountingSettingsDto>) =>
    setForm((prev) => (prev ? { ...prev, ...partial } : prev));

  const saveMutation = useMutation({
    mutationFn: (payload: UpdateAccountingSettingsRequest) => updateAccountingSettings(payload),
    onSuccess: (saved) => {
      toast.success('تنظیمات ذخیره شد', `نسخه ${digitsToPersian(saved.version)} — تاریخچه در بخش تاریخچه نسخه‌ها.`);
      setForm(saved);
      setChangeReason('');
      void queryClient.invalidateQueries({ queryKey: ['accounting-settings'] });
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 409) {
        toast.error('تعارض نسخه', 'کاربر دیگری تنظیمات را تغییر داده؛ فرم با آخرین نسخه به‌روزرسانی شد.');
        void settings.refetch();
      } else {
        toast.error('ذخیره تنظیمات انجام نشد', error instanceof ApiError ? error.message : undefined);
      }
    },
  });

  if (!canRead) {
    return <ErrorState title="مجوز مشاهده تنظیمات را ندارید" description="این صفحه نیازمند مجوز accounting:settings:read است." />;
  }

  if (settings.isLoading || !form) {
    return (
      <div className="space-y-3" aria-busy="true" role="status" data-testid="settings-loading">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-48 w-full" />
        <Skeleton className="h-48 w-full" />
      </div>
    );
  }

  if (settings.isError) {
    return <ErrorState title="بارگذاری تنظیمات ناموفق بود" onRetry={() => void settings.refetch()} />;
  }

  const handleSave = () => {
    saveMutation.mutate({
      defaultCurrencyCode: form.defaultCurrencyCode,
      calendarType: form.calendarType,
      fiscalYearStartMonth: form.fiscalYearStartMonth,
      fiscalYearStartDay: form.fiscalYearStartDay,
      decimalPlaces: form.decimalPlaces,
      roundingMode: form.roundingMode,
      voucherNumberingPolicy: form.voucherNumberingPolicy,
      allowNegativeBalancesPolicy: form.allowNegativeBalancesPolicy,
      approvalPolicy: form.approvalPolicy,
      defaultAccountTemplateId: form.defaultAccountTemplateId,
      lockDate: form.lockDate,
      allowUnbalancedDraft: form.allowUnbalancedDraft,
      autoNumberingEnabled: form.autoNumberingEnabled,
      requireSeparateApprover: form.requireSeparateApprover,
      requireSeparatePoster: form.requireSeparatePoster,
      changeReason: changeReason.trim() || null,
    });
  };

  return (
    <div className="space-y-4" data-testid="accounting-settings-page">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-lg font-black text-foreground">
            <Settings2 className="size-5 text-primary" aria-hidden />
            تنظیمات حسابداری
          </h1>
          <p className="mt-0.5 text-xs text-muted-foreground">
            نسخه فعلی: {digitsToPersian(form.version)}
            {form.lastModifiedAtUtc && ` · آخرین تغییر ${formatToPersianDateTime(form.lastModifiedAtUtc)}`}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={() => setHistoryOpen(true)} data-testid="open-history">
            <History className="size-3.5" aria-hidden />
            تاریخچه نسخه‌ها
          </Button>
          <Button variant="ghost" size="icon" onClick={() => void settings.refetch()} aria-label="بارگذاری مجدد">
            <RefreshCw className="size-4" aria-hidden />
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
        {/* واحد پول و دقت */}
        <Card>
          <CardHeader className="border-b border-border/60 pb-2.5"><h3 className="text-sm font-bold">واحد پول و دقت</h3></CardHeader>
          <CardContent className="grid grid-cols-1 gap-3 p-4 sm:grid-cols-2">
            <div>
              <label htmlFor="cur" className="mb-1 block text-xs font-semibold">ارز پایه *</label>
              <Input id="cur" dir="ltr" value={form.defaultCurrencyCode} onChange={(event) => patch({ defaultCurrencyCode: event.target.value.toUpperCase() })} data-testid="settings-currency" />
            </div>
            <div>
              <label htmlFor="dec" className="mb-1 block text-xs font-semibold">دقت اعشار</label>
              <select id="dec" className="h-9 w-full rounded-md border border-border bg-surface px-2 text-sm" value={form.decimalPlaces} onChange={(event) => patch({ decimalPlaces: Number(event.target.value) })} data-testid="settings-decimal">
                {[0, 1, 2, 3, 4, 5, 6, 7].map((value) => (
                  <option key={value} value={value}>{digitsToPersian(value)} رقم</option>
                ))}
              </select>
            </div>
            <div>
              <label htmlFor="round" className="mb-1 block text-xs font-semibold">سیاست گرد کردن</label>
              <select id="round" className="h-9 w-full rounded-md border border-border bg-surface px-2 text-sm" value={form.roundingMode} onChange={(event) => patch({ roundingMode: event.target.value })} data-testid="settings-rounding">
                {roundingOptions.map((option) => (
                  <option key={option.value} value={option.value}>{option.label}</option>
                ))}
              </select>
            </div>
            <div>
              <label htmlFor="neg" className="mb-1 block text-xs font-semibold">مانده منفی</label>
              <select id="neg" className="h-9 w-full rounded-md border border-border bg-surface px-2 text-sm" value={form.allowNegativeBalancesPolicy} onChange={(event) => patch({ allowNegativeBalancesPolicy: event.target.value })} data-testid="settings-negative">
                {negativeOptions.map((option) => (
                  <option key={option.value} value={option.value}>{option.label}</option>
                ))}
              </select>
            </div>
          </CardContent>
        </Card>

        {/* تقویم مالی */}
        <Card>
          <CardHeader className="border-b border-border/60 pb-2.5"><h3 className="text-sm font-bold">تقویم مالی</h3></CardHeader>
          <CardContent className="grid grid-cols-1 gap-3 p-4 sm:grid-cols-3">
            <div>
              <label htmlFor="cal" className="mb-1 block text-xs font-semibold">نوع تقویم</label>
              <select id="cal" className="h-9 w-full rounded-md border border-border bg-surface px-2 text-sm" value={form.calendarType} onChange={(event) => patch({ calendarType: event.target.value })} data-testid="settings-calendar">
                <option value="Persian">هجری شمسی</option>
                <option value="Gregorian">میلادی</option>
              </select>
            </div>
            <div>
              <label htmlFor="sm" className="mb-1 block text-xs font-semibold">ماه شروع سال</label>
              <select id="sm" className="h-9 w-full rounded-md border border-border bg-surface px-2 text-sm" value={form.fiscalYearStartMonth} onChange={(event) => patch({ fiscalYearStartMonth: Number(event.target.value) })}>
                {Array.from({ length: 12 }, (_, index) => index + 1).map((month) => (
                  <option key={month} value={month}>{digitsToPersian(month)}</option>
                ))}
              </select>
            </div>
            <div>
              <label htmlFor="sd" className="mb-1 block text-xs font-semibold">روز شروع</label>
              <select id="sd" className="h-9 w-full rounded-md border border-border bg-surface px-2 text-sm" value={form.fiscalYearStartDay} onChange={(event) => patch({ fiscalYearStartDay: Number(event.target.value) })}>
                {Array.from({ length: 31 }, (_, index) => index + 1).map((day) => (
                  <option key={day} value={day}>{digitsToPersian(day)}</option>
                ))}
              </select>
            </div>
            <div className="sm:col-span-3">
              <label htmlFor="lock" className="mb-1 block text-xs font-semibold">تاریخ قفل (LockDate) — اسناد قبل از این تاریخ ثبت نمی‌شوند</label>
              <Input id="lock" type="date" dir="ltr" value={form.lockDate ?? ''} onChange={(event) => patch({ lockDate: event.target.value || null })} data-testid="settings-lockdate" />
            </div>
          </CardContent>
        </Card>

        {/* شماره‌گذاری */}
        <Card>
          <CardHeader className="border-b border-border/60 pb-2.5"><h3 className="text-sm font-bold">شماره‌گذاری اسناد</h3></CardHeader>
          <CardContent className="space-y-3 p-4">
            <Switch checked={form.autoNumberingEnabled} onCheckedChange={(checked) => patch({ autoNumberingEnabled: checked })} label="شماره‌گذاری خودکار" data-testid="settings-autonumber" />
            <div>
              <label htmlFor="num" className="mb-1 block text-xs font-semibold">سیاست شماره‌گذاری</label>
              <select id="num" className="h-9 w-full rounded-md border border-border bg-surface px-2 text-sm" value={form.voucherNumberingPolicy} onChange={(event) => patch({ voucherNumberingPolicy: event.target.value })} data-testid="settings-numbering">
                {numberingOptions.map((option) => (
                  <option key={option.value} value={option.value}>{option.label}</option>
                ))}
              </select>
            </div>
          </CardContent>
        </Card>

        {/* تأیید اسناد */}
        <Card>
          <CardHeader className="border-b border-border/60 pb-2.5"><h3 className="text-sm font-bold">جریان تأیید اسناد</h3></CardHeader>
          <CardContent className="space-y-3 p-4">
            <div>
              <label htmlFor="app" className="mb-1 block text-xs font-semibold">سیاست تأیید</label>
              <select id="app" className="h-9 w-full rounded-md border border-border bg-surface px-2 text-sm" value={form.approvalPolicy} onChange={(event) => patch({ approvalPolicy: event.target.value })} data-testid="settings-approval">
                {approvalOptions.map((option) => (
                  <option key={option.value} value={option.value}>{option.label}</option>
                ))}
              </select>
            </div>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Switch checked={form.requireSeparateApprover} onCheckedChange={(checked) => patch({ requireSeparateApprover: checked })} label="تأییدکننده جدا از ایجادکننده" data-testid="settings-sep-approver" />
              <Switch checked={form.requireSeparatePoster} onCheckedChange={(checked) => patch({ requireSeparatePoster: checked })} label="ثبت‌کننده جدا از تأییدکننده" data-testid="settings-sep-poster" />
              <Switch checked={form.allowUnbalancedDraft} onCheckedChange={(checked) => patch({ allowUnbalancedDraft: checked })} label="اجازه پیش‌نویس نامتراز" data-testid="settings-unbalanced" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* دلیل تغییر + ذخیره */}
      {canUpdate ? (
        <Card>
          <CardContent className="space-y-3 p-4">
            <div>
              <label htmlFor="reason" className="mb-1 block text-xs font-semibold">دلیل تغییر (ثبت در تاریخچه)</label>
              <Textarea id="reason" rows={2} value={changeReason} onChange={(event) => setChangeReason(event.target.value)} placeholder="مثلاً تغییر سیاست گرد کردن بر اساس بخشنامه مالی" data-testid="settings-change-reason" />
            </div>
            <div className="flex items-center justify-between">
              <span className="text-[11px] text-muted-foreground">ذخیره با نسخه {digitsToPersian(form.version)} انجام می‌شود؛ در تعارض نسخه پیام و به‌روزرسانی دریافت می‌کنید.</span>
              <Button onClick={handleSave} disabled={saveMutation.isPending} data-testid="settings-save">
                {saveMutation.isPending ? 'در حال ذخیره…' : 'ذخیره تنظیمات'}
              </Button>
            </div>
          </CardContent>
        </Card>
      ) : (
        <p className="rounded-md border border-border/60 bg-muted/30 px-3 py-2 text-xs text-muted-foreground">برای تغییر تنظیمات مجوز accounting:settings:update لازم است.</p>
      )}

      {/* تاریخچه */}
      <Modal open={historyOpen} onOpenChange={setHistoryOpen} title="تاریخچه نسخه‌های تنظیمات" size="md">
        {history.isLoading ? (
          <Skeleton className="h-32 w-full" />
        ) : (history.data ?? []).length === 0 ? (
          <p className="py-4 text-center text-xs text-muted-foreground">تاریخچه‌ای ثبت نشده است</p>
        ) : (
          <ul className="max-h-96 space-y-2 overflow-y-auto" data-testid="settings-history">
            {(history.data ?? []).map((entry: AccountingSettingsHistoryDto) => (
              <li key={entry.id} className="flex items-center justify-between gap-2 rounded-md border border-border/60 px-3 py-2 text-xs">
                <span>
                  <Badge variant="secondary" className="me-1 text-[10px]">نسخه {digitsToPersian(entry.version)}</Badge>
                  {entry.changeReason || 'بدون شرح'}
                </span>
                <span className="shrink-0 text-[10px] text-muted-foreground">
                  {formatToPersianDateTime(entry.changedAtUtc)}
                  {entry.changedBy && ` · ${entry.changedBy.slice(0, 8)}`}
                </span>
              </li>
            ))}
          </ul>
        )}
      </Modal>
    </div>
  );
};
