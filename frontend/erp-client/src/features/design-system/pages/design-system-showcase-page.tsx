import * as React from 'react';
import { Rocket, Trash2, Plus, Search, UserPlus } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { IconButton } from '@/shared/components/ui/icon-button';
import { Input } from '@/shared/components/ui/input';
import { Textarea } from '@/shared/components/ui/textarea';
import { Badge } from '@/shared/components/ui/badge';
import { Avatar } from '@/shared/components/ui/avatar';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { Tabs } from '@/shared/components/ui/tabs';
import { Pagination } from '@/shared/components/ui/pagination';
import { Checkbox, Radio, Switch } from '@/shared/components/ui/selection-controls';
import { Select } from '@/shared/components/forms/select';
import { Combobox, MultiSelect, SearchableSelect } from '@/shared/components/forms/combobox';
import { NumberInput } from '@/shared/components/forms/number-input';
import { CurrencyInput } from '@/shared/components/forms/currency-input';
import { PasswordInput } from '@/shared/components/forms/password-input';
import { FormField } from '@/shared/components/forms/form-field';
import { DateRangePickerJalali } from '@/shared/components/forms/date-range-picker-jalali';
import { Modal } from '@/shared/components/ui/modal';
import { Drawer } from '@/shared/components/ui/drawer';
import { ConfirmDialog } from '@/shared/components/ui/confirm-dialog';
import { Tooltip } from '@/shared/components/ui/tooltip';
import { LoadingState } from '@/shared/components/feedback/loading-state';
import { EmptyState } from '@/shared/components/feedback/empty-state';
import { ErrorState } from '@/shared/components/feedback/error-state';
import { MoneyDisplay, DebitCreditDisplay, BalanceDisplay } from '@/shared/components/financial/money-display';
import { VoucherStatusBadge } from '@/shared/components/financial/voucher-status-badge';
import { JalaliDateDisplay } from '@/shared/components/financial/jalali-date-display';
import { VoucherInfoCard } from '@/shared/components/financial/voucher-info-card';
import { AccountTreeSelect, type AccountNode } from '@/shared/components/financial/account-tree-select';
import { DetailAccountSelect } from '@/shared/components/financial/detail-account-select';
import { FiscalPeriodPicker, type FiscalPeriod } from '@/shared/components/financial/fiscal-period';
import { formatMoney } from '@/shared/utils/money';
import { digitsToPersian } from '@/shared/utils/number';

export const DesignSystemShowcasePage: React.FC = () => {
  const [numberValue, setNumberValue] = React.useState<number | null>(1250000);
  const [currencyValue, setCurrencyValue] = React.useState<number | null>(9876543);
  const [negativeCurrency, setNegativeCurrency] = React.useState<number | null>(-75000);
  const [selectValue, setSelectValue] = React.useState<string>('t1');
  const [searchValue, setSearchValue] = React.useState<string | null>('a2');
  const [multiValue, setMultiValue] = React.useState<string[]>(['a1', 'a3']);
  const [range, setRange] = React.useState<{ from: string | null; to: string | null }>({ from: '2025-03-21', to: '2026-03-20' });
  const [treeValue, setTreeValue] = React.useState<string | null>('110101');
  const [detailValue, setDetailValue] = React.useState<string | null>('d1');
  const [periodValue, setPeriodValue] = React.useState<string | null>('1403');
  const [modalOpen, setModalOpen] = React.useState(false);
  const [drawerOpen, setDrawerOpen] = React.useState(false);
  const [confirmOpen, setConfirmOpen] = React.useState(false);
  const [switchOn, setSwitchOn] = React.useState(true);
  const [page, setPage] = React.useState(2);

  const accounts: readonly AccountNode[] = [
    { id: '1100', code: '1100', title: 'دارایی‌های جاری', parentId: null },
    { id: '1101', code: '1101', title: 'صندوق', parentId: '1100' },
    { id: '110101', code: '110101', title: 'صندوق مرکزی', parentId: '1101', selectable: true },
    { id: '1102', code: '1102', title: 'بانک', parentId: '1100' },
    { id: '110201', code: '110201', title: 'بانک ملت جاری', parentId: '1102', selectable: true },
  ];
  const details = [
    { id: 'd1', title: 'شرکت بازرگانی آریا', code: 'D-1024' },
    { id: 'd2', title: 'خریدار نقدی', code: 'D-0001' },
  ];
  const periods: readonly FiscalPeriod[] = [
    { id: '1403', title: 'دوره مالی ۱۴۰۳', status: 'closed' },
    { id: '1404', title: 'دوره مالی ۱۴۰۴', status: 'open' },
  ];

  return (
    <div className="mx-auto max-w-5xl space-y-10 p-6">
      <header className="space-y-2">
        <h1 className="text-2xl font-bold">Design System — Showcase</h1>
        <p className="text-sm text-muted-foreground bidi-safe">
          همه کامپوننت‌های پایه و ویژه مالی ERP با توکن‌های مشترک. مسیر مستندات: docs/frontend/design-system.md
        </p>
      </header>

      {/* Buttons */}
      <section className="space-y-3">
        <h2 className="text-lg font-bold">دکمه‌ها</h2>
        <div className="flex flex-wrap items-center gap-3">
          <Button><Plus className="size-4" aria-hidden />سند جدید</Button>
          <Button variant="outline">ذخیره پیش‌نویس</Button>
          <Button variant="destructive"><Trash2 className="size-4" aria-hidden />حذف</Button>
          <Button variant="secondary">دومین</Button>
          <Button variant="ghost">بدون پس‌زمینه</Button>
          <Button disabled>غیرفعال</Button>
          <Button loading>در حال ارسال</Button>
          <Tooltip content="جستجوی پیشرفته (Ctrl+K)">
            <IconButton variant="outline" aria-label="جستجو"><Search className="size-4" aria-hidden /></IconButton>
          </Tooltip>
        </div>
      </section>

      {/* Inputs */}
      <section className="space-y-4">
        <h2 className="text-lg font-bold">ورودی‌ها</h2>
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField id="sc-name" label="عنوان سند" required error={undefined} hint="مثال: سند خرید مهر ۱۴۰۴">
            <Input id="sc-name" placeholder="مثلاً سند تسعیر ارز" />
          </FormField>
          <FormField id="sc-error" label="فیلد با خطا" required error="این فیلد الزامی است">
            <Input id="sc-error" aria-invalid data-testid="invalid-input" />
          </FormField>
          <FormField id="sc-pass" label="رمز">
            <PasswordInput id="sc-pass" placeholder="••••••••" />
          </FormField>
          <FormField id="sc-num" label="عدد (فارسی/انگلیسی)" hint={`مقدار واقعی: ${numberValue ?? '—'}`}>
            <NumberInput id="sc-num" value={numberValue} onChange={setNumberValue} />
          </FormField>
          <FormField id="sc-cur" label="مبلغ ریال" hint={`مقدار ذخیره‌شده: ${currencyValue ?? '—'}`}>
            <CurrencyInput id="sc-cur" value={currencyValue} onChange={setCurrencyValue} />
          </FormField>
          <FormField id="sc-neg" label="مبلغ منفی (تعدیلی)" hint={negativeCurrency === null ? 'خالی' : formatMoney(negativeCurrency)}>
            <CurrencyInput id="sc-neg" value={negativeCurrency} onChange={setNegativeCurrency} allowNegative />
          </FormField>
          <FormField id="sc-text" label="توضیحات">
            <Textarea id="sc-text" placeholder="شرح سند…" />
          </FormField>
          <FormField id="sc-select" label="انتخاب ساده">
            <Select
              id="sc-select"
              value={selectValue}
              onChange={(e) => setSelectValue(e.target.value)}
              options={[
                { value: 't1', label: 'فروش' },
                { value: 't2', label: 'خرید' },
                { value: 't3', label: 'انتقال بین صندوق' },
              ]}
            />
          </FormField>
          <FormField id="sc-search" label="SearchableSelect">
            <SearchableSelect
              options={[
                { value: 'a1', label: 'مشتری پارسیان', description: 'C-1001' },
                { value: 'a2', label: 'تامین‌کننده کاویان', description: 'V-2002' },
                { value: 'a3', label: 'شرکت پارس‌تک', description: 'C-1003' },
              ]}
              value={searchValue}
              onChange={setSearchValue}
            />
          </FormField>
          <FormField id="sc-multi" label="MultiSelect">
            <MultiSelect
              options={[
                { value: 'a1', label: 'مرکز هزینه تهران' },
                { value: 'a2', label: 'مرکز هزینه کرج' },
                { value: 'a3', label: 'پروژه شمال' },
              ]}
              value={multiValue}
              onChange={setMultiValue}
            />
          </FormField>
          <FormField id="sc-combo" label="Combobox با ساخت مقدار جدید">
            <Combobox
              options={[
                { value: 'p1', label: 'پروژه تعمیرات' },
                { value: 'p2', label: 'پروژه توسعه' },
              ]}
              value={null}
              onChange={() => undefined}
              allowCreate
            />
          </FormField>
          <FormField id="sc-range" label="بازه شمسی">
            <DateRangePickerJalali from={range.from} to={range.to} onChange={setRange} />
          </FormField>
        </div>
      </section>

      {/* Financial */}
      <section className="space-y-4">
        <h2 className="text-lg font-bold">کامپوننت‌های مالی</h2>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="rounded-lg border p-4 space-y-3">
            <p className="text-sm font-bold">نمایش مبالغ</p>
            <p><MoneyDisplay amount={1234567890} options={{ withCurrency: true }} /></p>
            <p><MoneyDisplay amount={-42500} /></p>
            <p><MoneyDisplay amount={0} options={{ zeroAsDash: true }} /></p>
            <div className="flex items-center gap-3">
              <DebitCreditDisplay amount={850000} nature="debit" />
              <DebitCreditDisplay amount={850000} nature="credit" />
            </div>
            <div className="flex items-center gap-3">
              <BalanceDisplay balance={2500000} currency="ریال" />
              <BalanceDisplay balance={-180000} />
              <BalanceDisplay balance={0} />
            </div>
          </div>
          <div className="rounded-lg border p-4 space-y-3">
            <p className="text-sm font-bold">وضعیت‌ها و تاریخ</p>
            <div className="flex flex-wrap gap-2">
              <VoucherStatusBadge status="draft" />
              <VoucherStatusBadge status="pending" />
              <VoucherStatusBadge status="posted" />
              <VoucherStatusBadge status="reversed" />
            </div>
            <p><JalaliDateDisplay date="2025-01-05" format="short" /></p>
            <p><JalaliDateDisplay date="2025-01-05" format="long" /></p>
            <p><JalaliDateDisplay date="2025-01-05" format="dual" /></p>
            <p className="font-code">110201 / D-1024 / INV-2025-0007</p>
          </div>
          <div className="space-y-2 sm:col-span-2">
            <p className="text-sm font-bold">درخت حساب‌ها / تفصیلی / بازه مالی</p>
            <div className="grid gap-3 sm:grid-cols-3">
              <AccountTreeSelect nodes={accounts} value={treeValue} onChange={setTreeValue} />
              <DetailAccountSelect accounts={details} value={detailValue} onChange={setDetailValue} />
            </div>
            <FiscalPeriodPicker periods={periods} value={periodValue} onChange={setPeriodValue}
              customFrom={range.from} customTo={range.to} onCustomRangeChange={setRange} />
          </div>
          <div className="sm:col-span-2">
            <p className="text-sm font-bold mb-2">کارت سند</p>
            <VoucherInfoCard
              voucher={{ number: 'INV-2025-0007', date: '2025-01-05', status: 'posted', description: 'فروش نسیه به شرکت آریا', totalAmount: 1234567890 }}
            />
          </div>
        </div>
      </section>

      {/* Feedback & states */}
      <section className="space-y-4">
        <h2 className="text-lg font-bold">حالت‌ها و بازخورد</h2>
        <div className="grid gap-4 sm:grid-cols-3">
          <LoadingState message="در حال دریافت تراز آزمایشی…" />
          <EmptyState title="سندی ثبت نشده" description="اولین سند را ثبت کنید." />
          <ErrorState onRetry={() => undefined} />
        </div>
        <div className="flex flex-wrap gap-3">
          <Skeleton className="h-4 w-40" />
          <Skeleton shape="circle" className="size-9" />
        </div>
        <Pagination page={page} pageCount={12} onChange={setPage} />
      </section>

      {/* Overlays */}
      <section className="space-y-4">
        <h2 className="text-lg font-bold">پنجره‌ها</h2>
        <div className="flex flex-wrap gap-3">
          <Button variant="outline" onClick={() => setModalOpen(true)}>مودال فرم</Button>
          <Button variant="outline" onClick={() => setDrawerOpen(true)}>کشوی جزئیات</Button>
          <Button variant="destructive" onClick={() => setConfirmOpen(true)}>حذف سند</Button>
        </div>
        <Modal open={modalOpen} onOpenChange={setModalOpen} title="سند جدید" description="اطلاعات سند را وارد کنید.">
          <div className="space-y-3">
            <FormField id="modal-desc" label="شرح سند"><Input id="modal-desc" /></FormField>
            <div className="flex justify-start gap-2 pt-2">
              <Button onClick={() => setModalOpen(false)}>ثبت</Button>
              <Button variant="outline" onClick={() => setModalOpen(false)}>انصراف</Button>
            </div>
          </div>
        </Modal>
        <Drawer open={drawerOpen} onOpenChange={setDrawerOpen} title="جزئیات سند INV-2025-0007">
          <p className="text-sm leading-7 text-muted-foreground">
            جمع سند: {formatMoney(1234567890, { withCurrency: true })}
          </p>
        </Drawer>
        <ConfirmDialog
          open={confirmOpen}
          onOpenChange={setConfirmOpen}
          title="حذف سند"
          description="سند INV-2025-0007 حذف خواهد شد. این عملیات بازگشت‌پذیر نیست."
          actionLabel="حذف"
          onConfirm={() => setConfirmOpen(false)}
        />
      </section>

      {/* Misc */}
      <section className="space-y-4">
        <h2 className="text-lg font-bold">متفرقه</h2>
        <div className="flex flex-wrap items-center gap-4">
          <Badge variant="success">فعال</Badge>
          <Badge variant="warning">در انتظار</Badge>
          <Badge variant="info">اطلاع</Badge>
          <Avatar name="مدیر سیستم" />
          <Avatar name="سارا محمدی" size="lg" />
          <Tabs
            aria-label="نمای سند"
            items={[
              { value: 'list', label: 'لیست' },
              { value: 'tree', label: 'درختی' },
              { value: 'cards', label: 'کارت', icon: <Rocket className="size-4" aria-hidden /> },
            ]}
          />
          <Checkbox label="فقط اسناد قطعی" defaultChecked />
          <Radio label="بدهکار" name="sc-nature" defaultChecked />
          <Switch checked={switchOn} onCheckedChange={setSwitchOn} label="نمایش ارقام فارسی" />
          <Button variant="outline" size="sm"><UserPlus className="size-4" aria-hidden />دعوت کاربر</Button>
          <span className="text-xs text-muted-foreground">{digitsToPersian(1404)}</span>
        </div>
      </section>
    </div>
  );
};
