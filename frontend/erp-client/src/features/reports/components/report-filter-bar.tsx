/**
 * نوار فیلترهای مالی وابسته و هوشمند:
 * سال مالی → دوره مالی (وابسته) → بازه تاریخ؛ تغییر سال، دوره و تاریخ‌های نامعتبر
 * را پاک می‌کند؛ انتخاب دوره، بازه تاریخ را به مرزهای دوره واقعی تنظیم می‌کند؛
 * تغییر دستی تاریخ، دوره را پاک می‌کند. ابعاد مالی (تفصیلی/مرکز هزینه/پروژه/شخص)
 * از فهرست واقعی DimensionsController بارگذاری می‌شوند — بازسازی محلی ممنوع.
 * همه مقادیر در querystring صفحه نگه‌داری می‌شوند (updateParams یکجا).
 */
import * as React from 'react';
import { useQuery } from '@tanstack/react-query';
import { RotateCcw, X } from 'lucide-react';
import { Input } from '@/shared/components/ui/input';
import { Button } from '@/shared/components/ui/button';
import { getAllFiscalYears, getFiscalPeriods } from '@/core/accounting/fiscal.service';
import { getFinancialDimensions } from '@/core/accounting/reports.service';
import { DIMENSION_TYPE } from '@/core/accounting/reports.types';
import { getPostableAccounts } from '@/core/accounting/accounts.service';
import { useCompanyStore } from '@/core/company/company.store';
import type { AccountDto } from '@/core/accounting/accounts.types';
import type { FinancialDimensionTypeName } from '@/core/accounting/reports.types';

export type ReportFilterKind =
  | 'year'
  | 'period'
  | 'dates'
  | 'account'
  | 'parentAccount'
  | 'detail'
  | 'costCenter'
  | 'project'
  | 'party'
  | 'currency';

export interface ReportFilterBarProps {
  /** مقادیر فعلی از URL */
  values: {
    fiscalYearId?: string;
    fiscalPeriodId?: string;
    from?: string;
    to?: string;
    accountId?: string;
    accountFullCode?: string;
    accountName?: string;
    parentAccountId?: string;
    parentAccountFullCode?: string;
    parentAccountName?: string;
    detailId?: string;
    costCenterId?: string;
    projectId?: string;
    partyId?: string;
    currencyCode?: string;
  };
  /** به‌روزرسانی چند پارامتر یکجا (پاک‌سازی وابسته‌ها در همین تماس) */
  onValuesChange: (updates: Record<string, string | undefined>) => void;
  enabled: ReportFilterKind[];
  /** صفحه نمایشی برای aria */
  testIdPrefix?: string;
}

const dimensionKindToType: Partial<Record<ReportFilterKind, FinancialDimensionTypeName>> = {
  detail: 'DetailAccount',
  costCenter: 'CostCenter',
  project: 'Project',
  party: 'Party',
};

export const ReportFilterBar: React.FC<ReportFilterBarProps> = ({ values, onValuesChange, enabled, testIdPrefix = 'report-filter' }) => {
  const companyId = useCompanyStore((state) => state.activeCompany?.id);
  const has = (kind: ReportFilterKind) => enabled.includes(kind);

  const years = useQuery({ queryKey: ['fiscal-years', 'list', companyId], queryFn: getAllFiscalYears, staleTime: 1000 * 120 });
  const periods = useQuery({
    queryKey: ['fiscal-periods', values.fiscalYearId, companyId],
    queryFn: () => getFiscalPeriods(values.fiscalYearId!),
    enabled: Boolean(values.fiscalYearId),
    staleTime: 1000 * 60,
  });
  const accounts = useQuery({ queryKey: ['accounts', 'postable', companyId], queryFn: getPostableAccounts, staleTime: 1000 * 120 });

  const dimensionQueries: Record<string, { items: Array<{ id: string; code: string; name: string }> }> = {};
  for (const kind of ['detail', 'costCenter', 'project', 'party'] as ReportFilterKind[]) {
    const type = dimensionKindToType[kind]!;
    dimensionQueries[kind] = { items: [] };
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const query = useQuery({
      queryKey: ['dimensions', type, companyId],
      queryFn: () => getFinancialDimensions({ dimensionType: DIMENSION_TYPE[type], pageSize: 100, isActive: true }),
      enabled: has(kind),
      staleTime: 1000 * 120,
    });
    if (query.data) dimensionQueries[kind] = { items: query.data.items };
  }

  /* ── منطق وابستگی فیلترها ─────────────────────────────────────────── */
  const changeYear = (yearId: string) => {
    // سال جدید → دوره و بازه تاریخ قبلی نامعتبر: پاک شوند
    onValuesChange({ fiscalYearId: yearId || undefined, fiscalPeriodId: undefined, from: undefined, to: undefined });
  };

  const changePeriod = (periodId: string) => {
    if (!periodId) {
      onValuesChange({ fiscalPeriodId: undefined });
      return;
    }
    const period = (periods.data ?? []).find((item: { id: string; startDate: string; endDate: string }) => item.id === periodId);
    if (period) {
      // انتخاب دوره واقعی → بازه تاریخ به مرزهای دوره
      onValuesChange({ fiscalPeriodId: periodId, from: period.startDate, to: period.endDate });
    }
  };

  const changeDate = (key: 'from' | 'to', value: string) => {
    // تغییر دستی تاریخ → انتخاب دوره دیگر معنا ندارد: پاک شود
    onValuesChange({ [key]: value || undefined, fiscalPeriodId: undefined });
  };

  const resetAll = () => {
    onValuesChange({ fiscalYearId: undefined, fiscalPeriodId: undefined, from: undefined, to: undefined, accountId: undefined, accountFullCode: undefined, accountName: undefined, parentAccountId: undefined, parentAccountFullCode: undefined, parentAccountName: undefined, detailId: undefined, costCenterId: undefined, projectId: undefined, partyId: undefined, currencyCode: undefined });
  };

  const selectAccount = (account: AccountDto, prefix: 'accountId' | 'parentAccountId') => {
    onValuesChange({
      [prefix]: account.id,
      [`${prefix === 'accountId' ? 'accountFullCode' : 'parentAccountFullCode'}`]: account.fullCode,
      [`${prefix === 'accountId' ? 'accountName' : 'parentAccountName'}`]: account.name,
    });
  };

  const selectedAccountCode = values.accountFullCode ?? '';
  const selectedAccountName = values.accountName ?? '';
  const selectedParentCode = values.parentAccountFullCode ?? '';
  const selectedParentName = values.parentAccountName ?? '';

  return (
    <div className="flex flex-wrap items-end gap-2 rounded-lg border border-border/60 bg-surface px-3 py-2.5" data-testid={`${testIdPrefix}-bar`}>
      {has('year') && (
        <label className="flex flex-col gap-1 text-[11px] font-semibold" htmlFor={`${testIdPrefix}-year`}>
          سال مالی
          <select id={`${testIdPrefix}-year`} value={values.fiscalYearId ?? ''} onChange={(event) => changeYear(event.target.value)} className="h-9 w-40 rounded-md border border-border bg-surface px-2 text-xs" data-testid={`${testIdPrefix}-year`}>
            <option value="">همه سال‌ها</option>
            {(years.data ?? []).map((year) => (
              <option key={year.id} value={year.id}>سال مالی {year.yearNumber}</option>
            ))}
          </select>
        </label>
      )}
      {has('period') && (
        <label className="flex flex-col gap-1 text-[11px] font-semibold" htmlFor={`${testIdPrefix}-period`}>
          دوره مالی
          <select id={`${testIdPrefix}-period`} value={values.fiscalPeriodId ?? ''} onChange={(event) => changePeriod(event.target.value)} className="h-9 w-40 rounded-md border border-border bg-surface px-2 text-xs disabled:opacity-50" disabled={!values.fiscalYearId} data-testid={`${testIdPrefix}-period`}>
            <option value="">{values.fiscalYearId ? 'همه دوره‌ها' : 'ابتدا سال مالی را انتخاب کنید'}</option>
            {(periods.data ?? []).map((period: { id: string; name: string }) => (
              <option key={period.id} value={period.id}>{period.name}</option>
            ))}
          </select>
        </label>
      )}
      {has('dates') && (
        <>
          <label className="flex flex-col gap-1 text-[11px] font-semibold" htmlFor={`${testIdPrefix}-from`}>
            از تاریخ
            <Input id={`${testIdPrefix}-from`} type="date" dir="ltr" value={values.from ?? ''} onChange={(event) => changeDate('from', event.target.value)} className="h-9 w-36 text-xs" data-testid={`${testIdPrefix}-from`} />
          </label>
          <label className="flex flex-col gap-1 text-[11px] font-semibold" htmlFor={`${testIdPrefix}-to`}>
            تا تاریخ
            <Input id={`${testIdPrefix}-to`} type="date" dir="ltr" value={values.to ?? ''} onChange={(event) => changeDate('to', event.target.value)} className="h-9 w-36 text-xs" data-testid={`${testIdPrefix}-to`} />
          </label>
        </>
      )}
      {has('account') && (
        <AccountFilterSelect
          label="حساب"
          accounts={accounts.data ?? []}
          selectedId={values.accountId}
          selectedCode={selectedAccountCode}
          selectedName={selectedAccountName}
          onSelect={(account) => selectAccount(account, 'accountId')}
          onClear={() => onValuesChange({ accountId: undefined, accountFullCode: undefined, accountName: undefined })}
          testId={`${testIdPrefix}-account`}
        />
      )}
      {has('parentAccount') && (
        <AccountFilterSelect
          label="حساب کل (والد)"
          accounts={(accounts.data ?? []).filter((account) => account.hasChildren)}
          selectedId={values.parentAccountId}
          selectedCode={selectedParentCode}
          selectedName={selectedParentName}
          onSelect={(account) => selectAccount(account, 'parentAccountId')}
          onClear={() => onValuesChange({ parentAccountId: undefined, parentAccountFullCode: undefined, parentAccountName: undefined })}
          testId={`${testIdPrefix}-parent`}
        />
      )}
      {(['detail', 'costCenter', 'project', 'party'] as ReportFilterKind[]).filter(has).map((kind) => (
        <label key={kind} className="flex flex-col gap-1 text-[11px] font-semibold" htmlFor={`${testIdPrefix}-${kind}`}>
          {kind === 'detail' ? 'تفصیلی شناور' : kind === 'costCenter' ? 'مرکز هزینه' : kind === 'project' ? 'پروژه' : 'شخص'}
          <select id={`${testIdPrefix}-${kind}`} value={(values as Record<string, string | undefined>)[`${kind}Id`] ?? ''} onChange={(event) => onValuesChange({ [`${kind}Id`]: event.target.value || undefined } as Record<string, string | undefined>)} className="h-9 w-44 rounded-md border border-border bg-surface px-2 text-xs" data-testid={`${testIdPrefix}-${kind}`}>
            <option value="">همه</option>
            {dimensionQueries[kind]?.items.map((dimension) => (
              <option key={dimension.id} value={dimension.id}>{dimension.code} — {dimension.name}</option>
            ))}
          </select>
        </label>
      ))}
      {has('currency') && (
        <label className="flex flex-col gap-1 text-[11px] font-semibold" htmlFor={`${testIdPrefix}-currency`}>
          ارز
          <Input id={`${testIdPrefix}-currency`} value={values.currencyCode ?? ''} onChange={(event) => onValuesChange({ currencyCode: event.target.value.toUpperCase() || undefined })} placeholder="IRR" className="h-9 w-20 text-xs" maxLength={3} data-testid={`${testIdPrefix}-currency`} />
        </label>
      )}
      <Button variant="ghost" size="sm" onClick={resetAll} aria-label="پاک کردن همه فیلترها" data-testid={`${testIdPrefix}-reset`}>
        <RotateCcw className="size-3.5" aria-hidden />
        بازنشانی
      </Button>
    </div>
  );
};

/** انتخابگر حساب فیلتر — جست‌وجوی ساده روی حساب‌های قابل‌ثبت واقعی */
const AccountFilterSelect: React.FC<{
  label: string;
  accounts: AccountDto[];
  selectedId?: string;
  selectedCode: string;
  selectedName: string;
  onSelect: (account: AccountDto) => void;
  onClear: () => void;
  testId: string;
}> = ({ label, accounts, selectedId, selectedCode, selectedName, onSelect, onClear, testId }) => {
  const [term, setTerm] = React.useState('');
  if (selectedId) {
    return (
      <span className="flex flex-col gap-1 text-[11px] font-semibold">
        {label}
        <span className="flex items-center gap-1 rounded-md border border-border bg-surface px-2 py-1.5 text-xs" data-testid={`${testId}-selected`}>
          <span dir="ltr" className="font-code text-[11px] text-muted-foreground">{selectedCode}</span>
          <span className="min-w-0 max-w-40 truncate">{selectedName}</span>
          <Button type="button" variant="ghost" size="icon" className="size-5" onClick={onClear} aria-label={`پاک کردن ${label}`}>
            <X className="size-3" aria-hidden />
          </Button>
        </span>
      </span>
    );
  }
  const filtered = term ? accounts.filter((account) => account.fullCode.includes(term) || account.name.includes(term)).slice(0, 20) : accounts.slice(0, 20);
  return (
    <label className="flex flex-col gap-1 text-[11px] font-semibold" htmlFor={`${testId}-search`}>
      {label}
      <input
        id={`${testId}-search`}
        value={term}
        onChange={(event) => setTerm(event.target.value)}
        placeholder="جست‌وجوی کد یا نام…"
        list={`${testId}-options`}
        className="h-9 w-44 rounded-md border border-border bg-surface px-2 text-xs"
        data-testid={testId}
      />
      <datalist id={`${testId}-options`}>
        {filtered.map((account) => (
          <option key={account.id} value={account.fullCode}>{account.name}</option>
        ))}
      </datalist>
      {/* انتخاب با کد کامل — مطابقت دقیق */}
      <input type="hidden" data-testid={`${testId}-apply`} />
      <button
        type="button"
        className="hidden"
        onClick={() => {
          const match = accounts.find((account) => account.fullCode === term.trim());
          if (match) onSelect(match);
        }}
      />
    </label>
  );
};
