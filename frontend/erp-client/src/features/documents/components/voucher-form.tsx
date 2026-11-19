/**
 * فرم ایجاد/ویرایش سند حسابداری — برای کار روزانه حسابدار:
 * جدول ردیف‌ها با ویرایش درجا، انتخاب حساب با جست‌وجو از حساب‌های قابل‌ثبت واقعی،
 * مبالغ decimal-safe (decimal.js؛ بدون float)، جمع بدهکار/بستانکار/اختلاف دقیق،
 * کپی/حذف/جابه‌جایی ردیف، کنترل اعتبار مطابق validatorهای backend و خطای دوره بسته.
 * محاسبات client فقط بازخورد سریع‌اند؛ backend مرجع نهایی است.
 */
import * as React from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, ArrowDown, ArrowUp, Check, Copy, Info, Plus, Save, Trash2, Upload } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Textarea } from '@/shared/components/ui/textarea';
import { Card, CardContent, CardHeader } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import { Tooltip } from '@/shared/components/ui/tooltip';
import { AccountLinePicker } from '@/features/documents/components/account-line-picker';
import {
  computeTotals,
  createDocument,
  detailToLineDrafts,
  formatAmount,
  parseAmountInput,
  submitDocument,
  updateDocument,
  validateVoucherForm,
} from '@/core/accounting/documents.service';
import type { DocumentDetailDto } from '@/core/accounting/documents.types';
import type { LineDraft } from '@/core/accounting/documents.service';
import { getAllFiscalYears, getFiscalPeriods } from '@/core/accounting/fiscal.service';
import type { FiscalPeriodDto, FiscalYearDto } from '@/core/accounting/fiscal.types';
import { useAuthStore } from '@/core/auth/auth.store';
import { PERMISSIONS } from '@/core/auth/permissions';
import { ApiError } from '@/core/errors/problem-details';
import { toast } from '@/core/notifications/toast';
import { cn } from '@/shared/utils/cn';
import { useUnsavedChangesGuard } from '@/shared/hooks/use-unsaved-changes';

export interface VoucherFormProps {
  /** ویرایش پیش‌نویس موجود */
  editingDocument?: DocumentDetailDto | null;
  onSaved?: (documentId: string) => void;
}

/** خطای وضعیت دوره برای نمایش — از دوره‌های واقعی سال انتخابی */
function closedPeriodCheck(date: string, periods: FiscalPeriodDto[]): 'closed-period' | null {
  if (!date) return null;
  const target = date;
  for (const period of periods) {
    if (target >= period.startDate && target <= period.endDate && period.status !== 'Open') {
      return 'closed-period';
    }
  }
  return null;
}

export const VoucherForm: React.FC<VoucherFormProps> = ({ editingDocument, onSaved }) => {
  const queryClient = useQueryClient();
  const hasAny = useAuthStore((state) => state.hasAnyPermission);
  const canSubmit = hasAny([PERMISSIONS.documentsSubmit]);

  const [documentDate, setDocumentDate] = React.useState(editingDocument?.documentDate ?? '');
  const [description, setDescription] = React.useState(editingDocument?.description ?? '');
  const [referenceNumber, setReferenceNumber] = React.useState(editingDocument?.referenceNumber ?? '');
  const [fiscalYearId, setFiscalYearId] = React.useState(editingDocument?.fiscalYearId ?? '');
  const [lines, setLines] = React.useState<LineDraft[]>(editingDocument ? detailToLineDrafts(editingDocument) : [emptyLine()]);
  const [showErrors, setShowErrors] = React.useState(false);
  const [saving, setSaving] = React.useState(false);

  const years = useQuery({ queryKey: ['fiscal-years', 'list'], queryFn: getAllFiscalYears, staleTime: 1000 * 120 });
  const periods = useQuery({
    queryKey: ['fiscal-periods', fiscalYearId],
    queryFn: () => getFiscalPeriods(fiscalYearId),
    enabled: Boolean(fiscalYearId),
    staleTime: 1000 * 60,
  });

  React.useEffect(() => {
    // انتخاب پیش‌فرض: سال مالی باز سال جاری
    if (!fiscalYearId && years.data) {
      const openYear = years.data.find((year: FiscalYearDto) => year.status === 'Open') ?? years.data[0];
      if (openYear) setFiscalYearId(openYear.id);
    }
  }, [years.data, fiscalYearId]);

  /* فاز ۹ — تشخیص dirty برای محافظ خروج: سریال‌سازی نرمال‌شده بدون keyهای نویزی */
  const formSignature = React.useMemo(
    () => JSON.stringify({
      documentDate, description: description.trim(), referenceNumber: referenceNumber.trim(), fiscalYearId,
      lines: lines.map((line) => [line.accountId ?? '', line.side, line.amount || '0', line.description.trim()]),
    }),
    [documentDate, description, referenceNumber, fiscalYearId, lines],
  );
  const baselineSignature = React.useMemo(
    () => JSON.stringify({
      documentDate: editingDocument?.documentDate ?? '', description: editingDocument?.description ?? '',
      referenceNumber: editingDocument?.referenceNumber ?? '', fiscalYearId: editingDocument?.fiscalYearId ?? '',
      lines: editingDocument ? detailToLineDrafts(editingDocument).map((line) => [line.accountId ?? '', line.side, line.amount || '0', line.description.trim()]) : [['', 'Debit', '0', '']],
    }),
    [editingDocument],
  );
  const isDirty = formSignature !== baselineSignature;
  const { guardDialog } = useUnsavedChangesGuard({ dirty: isDirty, title: 'خروج از سند نیمه‌کاره' });

  const totals = React.useMemo(() => computeTotals(lines), [lines]);
  const periodCheck = React.useMemo(
    () => closedPeriodCheck(documentDate, periods.data ?? []),
    [documentDate, periods.data],
  );

  const validation = React.useMemo(
    () => validateVoucherForm(description, documentDate, lines, periodCheck),
    [description, documentDate, lines, periodCheck],
  );

  /* ── عملیات ردیف‌ها ─────────────────────────────────────────────── */
  const updateLine = (key: string, patch: Partial<LineDraft>) =>
    setLines((prev) => prev.map((line) => (line.key === key ? { ...line, ...patch } : line)));

  const addLine = () => setLines((prev) => [...prev, emptyLine()]);
  const duplicateLine = (key: string) =>
    setLines((prev) => {
      const index = prev.findIndex((line) => line.key === key);
      if (index < 0) return prev;
      const copy = { ...prev[index], key: `${Date.now()}-${Math.random()}` };
      return [...prev.slice(0, index + 1), copy, ...prev.slice(index + 1)];
    });
  const removeLine = (key: string) =>
    setLines((prev) => (prev.length > 1 ? prev.filter((line) => line.key !== key) : [emptyLine()]));

  // جابه‌جایی در فرم پیش‌نویس مجاز است (backend ترتیب را با جایگزینی کل ردیف‌ها می‌پذیرد)
  const moveLine = (key: string, direction: -1 | 1) =>
    setLines((prev) => {
      const index = prev.findIndex((line) => line.key === key);
      const target = index + direction;
      if (index < 0 || target < 0 || target >= prev.length) return prev;
      const next = [...prev];
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });

  /* ── ذخیره/ارسال ────────────────────────────────────────────────── */
  const buildRequest = () => ({
    documentDate,
    description: description.trim(),
    lines: lines
      .filter((line) => line.accountId)
      .map((line) => ({
        accountId: line.accountId!,
        side: line.side,
        amount: line.amount || '0',
        description: line.description.trim(),
        costCenterId: line.costCenterId ?? null,
        projectId: line.projectId ?? null,
        detailAccountId: line.detailAccountId ?? null,
        foreignCurrencyCode: line.foreignCurrencyCode ?? null,
        foreignAmount: null,
        exchangeRate: 1.0,
      })),
    fiscalYearId: fiscalYearId || null,
    documentType: 'General' as const,
    referenceNumber: referenceNumber || null,
    idempotencyKey: crypto.randomUUID(),
  });

  /* refs برای انتقال فوکوس به فیلد خطا (فاز ۹ — accessibility) */
  const dateRef = React.useRef<HTMLInputElement>(null);
  const descriptionRef = React.useRef<HTMLTextAreaElement>(null);
  const lineRefs = React.useRef<Array<HTMLDivElement | null>>([]);

  const focusFirstErrorField = React.useCallback(() => {
    if (!documentDate) { dateRef.current?.focus(); return; }
    if (!description.trim()) { descriptionRef.current?.focus(); return; }
    const badLineIndex = lines.findIndex((line) => !line.accountId || !line.amount || Number(line.amount) === 0);
    if (badLineIndex >= 0) {
      const firstInput = lineRefs.current[badLineIndex]?.querySelector('input, button') as HTMLInputElement | null;
      firstInput?.focus();
    }
  }, [documentDate, description, lines]);

  const save = async (thenSubmit: boolean): Promise<string | null> => {
    setShowErrors(true);
    if (!validation.isValid) { focusFirstErrorField(); return null; }
    setSaving(true);
    try {
      let documentId: string | null = editingDocument?.id ?? null;
      if (editingDocument) {
        await updateDocument(editingDocument.id, buildRequest());
      } else {
        const created = await createDocument(buildRequest());
        documentId = created.id;
      }
      if (thenSubmit) {
        await submitDocument(documentId!);
        toast.success('سند ارسال شد', 'سند در جریان تأیید قرار گرفت (PendingApproval).');
      } else {
        toast.success('پیش‌نویس ذخیره شد', 'برای ثبت قطعی، سند را ارسال و تأیید کنید.');
      }
      void queryClient.invalidateQueries({ queryKey: ['documents'] });
      onSaved?.(documentId!);
      return documentId;
    } catch (error) {
      const message = error instanceof ApiError ? error.message : 'خطای شبکه — سند ذخیره نشد، دوباره تلاش کنید.';
      toast.error(thenSubmit ? 'ارسال سند انجام نشد' : 'ذخیره پیش‌نویس انجام نشد', message);
      return null;
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-3" data-testid="voucher-form">
      {/* سربرگ */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between border-b border-border/60 pb-3">
          <h2 className="text-sm font-bold">{editingDocument ? `ویرایش پیش‌نویس سند ${editingDocument.documentNumber}` : 'سند حسابداری جدید'}</h2>
          {editingDocument && <Badge variant="secondary">{editingDocument.status === 'Draft' ? 'پیش‌نویس' : editingDocument.status}</Badge>}
        </CardHeader>
        <CardContent className="grid grid-cols-1 gap-3 p-4 sm:grid-cols-2 lg:grid-cols-4">
          <div>
            <label htmlFor="v-date" className="mb-1 block text-xs font-semibold">تاریخ سند *</label>
            <Input id="v-date" type="date" dir="ltr" value={documentDate} onChange={(event) => setDocumentDate(event.target.value)} data-testid="voucher-date" ref={dateRef} />
          </div>
          <div>
            <label htmlFor="v-year" className="mb-1 block text-xs font-semibold">سال مالی</label>
            <select id="v-year" className="h-9 w-full rounded-md border border-border bg-surface px-2 text-sm" value={fiscalYearId} onChange={(event) => setFiscalYearId(event.target.value)} data-testid="voucher-fiscal-year">
              {(years.data ?? []).map((year) => (
                <option key={year.id} value={year.id}>سال مالی {year.yearNumber} — {year.status === 'Open' ? 'باز' : year.status === 'Draft' ? 'پیش‌نویس' : 'بسته'}</option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="v-ref" className="mb-1 block text-xs font-semibold">شماره ارجاع</label>
            <Input id="v-ref" value={referenceNumber} onChange={(event) => setReferenceNumber(event.target.value)} maxLength={100} placeholder="اختیاری" />
          </div>
          <div className="sm:col-span-2 lg:col-span-1">
            <label htmlFor="v-desc" className="mb-1 block text-xs font-semibold">شرح سند *</label>
            <Textarea id="v-desc" rows={1} value={description} onChange={(event) => setDescription(event.target.value)} maxLength={500} data-testid="voucher-description" ref={descriptionRef} />
          </div>
        </CardContent>
      </Card>

      {/* ردیف‌ها */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between border-b border-border/60 pb-2.5">
          <h3 className="text-sm font-bold">ردیف‌های سند</h3>
          <Button variant="outline" size="sm" onClick={addLine} data-testid="add-row">
            <Plus className="size-3.5" aria-hidden />
            افزودن ردیف
          </Button>
        </CardHeader>
        <CardContent className="space-y-2 p-3">
          {showErrors && validation.errors.length > 0 && (
            <div className="rounded-md border border-danger/40 bg-danger/5 px-3 py-2" role="alert" data-testid="form-errors">
              <p className="flex items-center gap-1.5 text-xs font-bold text-danger"><AlertTriangle className="size-3.5" aria-hidden />اعتبارسنجی سند:</p>
              <ul className="mt-1 space-y-0.5 text-[11px] text-danger">
                {validation.errors.map((error) => (
                  <li key={`${error.type}-${error.message}`}>• {error.message}</li>
                ))}
              </ul>
            </div>
          )}
          {periodCheck === 'closed-period' && (
            <p className="rounded-md border border-warning/40 bg-warning/5 px-3 py-2 text-[11px] text-warning" role="alert" data-testid="closed-period-warning">
              دوره مالی این تاریخ باز نیست — backend سند را با DocumentDateOutsideOpenPeriod رد می‌کند.
            </p>
          )}

          <div className="space-y-2" data-testid="voucher-lines">
            {lines.map((line, index) => (
              <LineRow
                key={line.key}
                index={index}
                containerRef={(node) => { lineRefs.current[index] = node; }}
                line={line}
                canMoveUp={index > 0}
                canMoveDown={index < lines.length - 1}
                onChange={(patch) => updateLine(line.key, patch)}
                onDuplicate={() => duplicateLine(line.key)}
                onRemove={() => removeLine(line.key)}
                onMove={(direction) => moveLine(line.key, direction)}
                showError={showErrors}
              />
            ))}
          </div>

          {/* جمع‌ها — decimal-safe */}
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-border/60 bg-muted/30 px-3 py-2.5" data-testid="totals-bar">
            <div className="flex flex-wrap items-center gap-4 text-xs">
              <span>جمع بدهکار: <strong dir="ltr" className="tabular-nums" data-testid="total-debit">{formatAmount(totals.totalDebit)}</strong></span>
              <span>جمع بستانکار: <strong dir="ltr" className="tabular-nums" data-testid="total-credit">{formatAmount(totals.totalCredit)}</strong></span>
              <span>
                اختلاف: <strong dir="ltr" className={cn('tabular-nums', totals.balanced ? 'text-success' : 'text-danger')} data-testid="total-difference">{totals.balanced ? '0' : formatAmount(totals.difference)}</strong>
              </span>
            </div>
            {totals.balanced ? (
              <Badge variant="success" data-testid="balanced-badge"><Check className="size-3" aria-hidden />متوازن</Badge>
            ) : (
              <Badge variant="destructive" data-testid="unbalanced-badge">نامتوازن</Badge>
            )}
          </div>
        </CardContent>
      </Card>

      {guardDialog}

      {/* عملیات */}
      <div className="flex flex-wrap items-center gap-2">
        <Button onClick={() => void save(false)} disabled={saving} data-testid="save-draft">
          <Save className="size-3.5" aria-hidden />
          {saving ? 'در حال ذخیره…' : 'ذخیره پیش‌نویس'}
        </Button>
        {canSubmit && !editingDocument?.status?.startsWith('Posted') && (
          <Button onClick={() => void save(true)} disabled={saving} data-testid="save-submit">
            <Upload className="size-3.5" aria-hidden />
            ذخیره و ارسال برای تأیید
          </Button>
        )}
        <Tooltip content="محاسبات جمع/اختلاف فقط بازخورد سریع است؛ backend مرجع نهایی توازن و ثبت سند است.">
          <span className="flex items-center gap-1 text-[11px] text-muted-foreground"><Info className="size-3.5" aria-hidden />محاسبات نمایشی — مرجع نهایی backend</span>
        </Tooltip>
      </div>
    </div>
  );
};

/* ── ردیف ──────────────────────────────────────────────────────────── */

interface LineRowProps {
  index: number;
  containerRef?: (node: HTMLDivElement | null) => void;
  line: LineDraft;
  canMoveUp: boolean;
  canMoveDown: boolean;
  onChange: (patch: Partial<LineDraft>) => void;
  onDuplicate: () => void;
  onRemove: () => void;
  onMove: (direction: -1 | 1) => void;
  showError: boolean;
}

const LineRow: React.FC<LineRowProps> = ({ index, containerRef, line, canMoveUp, canMoveDown, onChange, onDuplicate, onRemove, onMove, showError }) => {
  const sideError = showError && (!line.accountId || !line.amount || line.amount === 'NaN' || Number(line.amount) === 0 ? 'ردیف ناقص' : null);

  return (
    <div ref={containerRef} className="grid grid-cols-1 items-start gap-1.5 rounded-md border border-border/40 p-2 sm:grid-cols-[2rem_minmax(0,1fr)_minmax(0,1fr)_7rem_7rem_auto]" data-testid={`line-row-${index + 1}`}>
      <span className="pt-1.5 text-center text-[11px] text-muted-foreground tabular-nums">{index + 1}</span>

      {/* حساب */}
      <div className="min-w-0">
        <AccountLinePicker
          testId={`line-account-picker-${index + 1}`}
          selectedFullCode={line.accountFullCode}
          selectedName={line.accountName}
          onSelect={(account) => onChange({ accountId: account.id, accountFullCode: account.fullCode, accountName: account.name, requireCostCenter: account.requireCostCenter, requireProject: account.requireProject, requireDetailAccount: account.requireDetailAccount, detailAccountId: null })}
          onClear={() => onChange({ accountId: null, accountFullCode: '', accountName: '' })}
        />
        {line.accountId && (line.requireCostCenter || line.requireProject || line.requireDetailAccount) && (
          <span className="mt-1 flex flex-wrap gap-1" data-testid={`line-dimensions-${index + 1}`}>
            {line.requireCostCenter && <Badge variant="warning" className="text-[9px]">مرکز هزینه الزامی</Badge>}
            {line.requireProject && <Badge variant="warning" className="text-[9px]">پروژه الزامی</Badge>}
            {line.requireDetailAccount && <Badge variant="warning" className="text-[9px]">تفصیلی الزامی</Badge>}
          </span>
        )}
      </div>

      {/* شرح */}
      <Input
        value={line.description}
        onChange={(event) => onChange({ description: event.target.value })}
        placeholder="شرح ردیف *"
        className="h-8 text-xs"
        aria-label={`شرح ردیف ${index + 1}`}
      />

      {/* بدهکار */}
      <div>
        <Input
          dir="ltr"
          inputMode="decimal"
          value={line.side === 'Debit' ? line.amountRaw : ''}
          onChange={(event) => {
            const parsed = parseAmountInput(event.target.value);
            onChange({ side: 'Debit', amountRaw: event.target.value, amount: parsed === 'NaN' ? '' : parsed });
          }}
          onFocus={(event) => event.target.select()}
          placeholder="بدهکار"
          className={cn('h-8 text-xs tabular-nums', line.side === 'Debit' && 'border-primary/60')}
          aria-label={`مبلغ بدهکار ردیف ${index + 1}`}
          data-testid={`line-debit-${index + 1}`}
        />
        {showError && sideError && line.side !== 'Credit' && <p className="mt-0.5 text-[10px] text-danger" role="alert">{sideError}</p>}
      </div>

      {/* بستانکار */}
      <div>
        <Input
          dir="ltr"
          inputMode="decimal"
          value={line.side === 'Credit' ? line.amountRaw : ''}
          onChange={(event) => {
            const parsed = parseAmountInput(event.target.value);
            onChange({ side: 'Credit', amountRaw: event.target.value, amount: parsed === 'NaN' ? '' : parsed });
          }}
          onFocus={(event) => event.target.select()}
          placeholder="بستانکار"
          className={cn('h-8 text-xs tabular-nums', line.side === 'Credit' && 'border-primary/60')}
          aria-label={`مبلغ بستانکار ردیف ${index + 1}`}
          data-testid={`line-credit-${index + 1}`}
        />
        {showError && sideError && line.side !== 'Debit' && <p className="mt-0.5 text-[10px] text-danger" role="alert">{sideError}</p>}
      </div>

      {/* عملیات */}
      <div className="flex items-center justify-end gap-0.5">
        <Button variant="ghost" size="icon" className="size-6" disabled={!canMoveUp} onClick={() => onMove(-1)} aria-label={`جابه‌جایی ردیف ${index + 1} به بالا`}>
          <ArrowUp className="size-3" aria-hidden />
        </Button>
        <Button variant="ghost" size="icon" className="size-6" disabled={!canMoveDown} onClick={() => onMove(1)} aria-label={`جابه‌جایی ردیف ${index + 1} به پایین`}>
          <ArrowDown className="size-3" aria-hidden />
        </Button>
        <Button variant="ghost" size="icon" className="size-6" onClick={onDuplicate} aria-label={`کپی ردیف ${index + 1}`} data-testid={`copy-row-${index + 1}`}>
          <Copy className="size-3" aria-hidden />
        </Button>
        <Button variant="ghost" size="icon" className="size-6 text-danger" onClick={onRemove} aria-label={`حذف ردیف ${index + 1}`} data-testid={`delete-row-${index + 1}`}>
          <Trash2 className="size-3" aria-hidden />
        </Button>
      </div>
    </div>
  );
};

function emptyLine(): LineDraft {
  return {
    key: `${Date.now()}-${Math.random()}`,
    accountId: null,
    accountFullCode: '',
    accountName: '',
    side: 'Debit',
    amountRaw: '',
    amount: '',
    description: '',
    requireCostCenter: false,
    requireProject: false,
    requireDetailAccount: false,
  };
}
