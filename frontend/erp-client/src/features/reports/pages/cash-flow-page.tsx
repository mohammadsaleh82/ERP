/**
 * جریان وجوه نقد — GET /accounting/reports/cash-flow (روش مستقیم)
 * فقط داده‌های طبقه‌بندی‌شده backend (CashFlowCategory) — استنتاج از مانده بانک ممنوع.
 * هشدار cashAccountsConfigured + یادداشت رسمی backend + مدیریت طبقه‌بندی حساب‌ها (PUT).
 */
import * as React from 'react';
import { PermissionGuard } from '@/app/guards/permission-guard';

import { useQuery } from '@tanstack/react-query';
import { Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip as ChartTooltip, XAxis, YAxis } from 'recharts';
import { AlertTriangle, Banknote, Settings2 } from 'lucide-react';
import { Card, CardContent, CardHeader } from '@/shared/components/ui/card';
import { Button } from '@/shared/components/ui/button';
import { BalanceCell } from '@/features/reports/components/financial-table';
import { ReportPageShell } from '@/features/reports/components/report-page-shell';
import { useReportFilters } from '@/features/reports/use-report-filters';
import { getCashFlow, getCashFlowClassifications, upsertCashFlowClassification } from '@/core/accounting/statements.service';
import { getPostableAccounts } from '@/core/accounting/accounts.service';
import { formatAmount } from '@/core/accounting/documents.service';
import { formatToPersianDate } from '@/shared/utils/date';
import { PERMISSIONS } from '@/core/auth/permissions';
import { useAuthStore } from '@/core/auth/auth.store';
import { toast } from '@/core/notifications/toast';
import { CASH_FLOW_CATEGORY_LABELS } from '@/core/accounting/statements.types';
import type { CashFlowCategoryName } from '@/core/accounting/statements.types';

const CATEGORY_OPTIONS: CashFlowCategoryName[] = ['Operating', 'Investing', 'Financing'];

/** مدیریت طبقه‌بندی جریان نقد — PUT /cash-flow-classifications با مجوز reports:classify */
const ClassificationManager: React.FC = () => {
  const canClassify = useAuthStore((state) => state.hasPermission(PERMISSIONS.reportsClassify));
  const classifications = useQuery({ queryKey: ['reports', 'cash-flow-classifications'], queryFn: getCashFlowClassifications });
  const accounts = useQuery({ queryKey: ['accounts', 'postable', 'cf-manager'], queryFn: getPostableAccounts, staleTime: 1000 * 120 });

  if (!canClassify) return null;
  const classified = classifications.data ?? [];

  return (
    <Card className="report-no-print">
      <CardHeader className="flex items-center gap-2 border-b border-border/60 pb-2.5 text-sm font-bold">
        <Settings2 className="size-4 text-primary" aria-hidden />
        طبقه‌بندی جریان نقد حساب‌ها
        <span className="text-[11px] font-normal text-muted-foreground">— منبع رسمی گزارش جریان نقد؛ بدون طبقه‌بندی حساب در گزارش نمی‌آید</span>
      </CardHeader>
      <CardContent className="p-0">
        {classified.length === 0 ? (
          <p className="py-4 text-center text-xs text-muted-foreground">هنوز حسابی طبقه‌بندی نشده است — از جدول زیر حساب‌های صندوق/بانک را طبقه‌بندی کنید.</p>
        ) : (
          <table className="w-full text-xs" aria-label="طبقه‌بندی‌های جریان نقد فعلی">
            <thead className="bg-muted/40 text-muted-foreground">
              <tr>
                <th scope="col" className="px-2.5 py-2 text-start font-semibold">حساب</th>
                <th scope="col" className="px-2.5 py-2 text-start font-semibold">طبقه فعلی</th>
              </tr>
            </thead>
            <tbody>
              {classified.map((item) => {
                const account = (accounts.data ?? []).find((row) => row.id === item.accountId);
                return (
                  <tr key={item.id} className="border-t border-border/40" data-testid={`cf-class-${item.accountId}`}>
                    <td className="px-2.5 py-2">{account ? `${account.fullCode} ${account.name}` : item.accountId}</td>
                    <td className="px-2.5 py-2">{CASH_FLOW_CATEGORY_LABELS[item.category]}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
        <div className="border-t border-border/40 p-2.5">
          <p className="mb-1.5 text-[11px] font-semibold text-muted-foreground">ثبت/تغییر طبقه‌بندی حساب:</p>
          <AccountClassifyForm disabledAccounts={new Set(classified.map((item) => item.accountId))} />
        </div>
      </CardContent>
    </Card>
  );
};

const AccountClassifyForm: React.FC<{ disabledAccounts: Set<string> }> = ({ disabledAccounts }) => {
  const accounts = useQuery({ queryKey: ['accounts', 'postable', 'cf-manager'], queryFn: getPostableAccounts, staleTime: 1000 * 120 });
  const [accountId, setAccountId] = React.useState('');
  const [category, setCategory] = React.useState<CashFlowCategoryName>('Operating');
  const [saving, setSaving] = React.useState(false);

  const submit = async () => {
    if (!accountId) return;
    setSaving(true);
    try {
      await upsertCashFlowClassification(accountId, category);
      toast.success('طبقه‌بندی ثبت شد', 'گزارش جریان نقد با طبقه‌بندی جدید تولید می‌شود');
    } catch {
      toast.error('ثبت طبقه‌بندی ناموفق بود');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="flex flex-wrap items-center gap-2">
      <select value={accountId} onChange={(event) => setAccountId(event.target.value)} className="h-9 w-56 rounded-md border border-border bg-surface px-2 text-xs" aria-label="انتخاب حساب" data-testid="cf-classify-account">
        <option value="">حساب…</option>
        {(accounts.data ?? []).filter((account) => !disabledAccounts.has(account.id)).map((account) => (
          <option key={account.id} value={account.id}>{account.fullCode} — {account.name}</option>
        ))}
      </select>
      <select value={category} onChange={(event) => setCategory(event.target.value as CashFlowCategoryName)} className="h-9 w-40 rounded-md border border-border bg-surface px-2 text-xs" aria-label="طبقه جریان نقد" data-testid="cf-classify-category">
        {CATEGORY_OPTIONS.map((option) => (
          <option key={option} value={option}>{CASH_FLOW_CATEGORY_LABELS[option]}</option>
        ))}
      </select>
      <Button size="sm" variant="outline" disabled={!accountId || saving} onClick={() => void submit()} data-testid="cf-classify-submit">ثبت طبقه‌بندی</Button>
    </div>
  );
};

const CashFlowContent: React.FC = () => {
  const { values, updateFilters, setPage, companyId } = useReportFilters();
  const [enabled, setEnabled] = React.useState(false);

  const report = useQuery({
    queryKey: ['reports', 'cash-flow', companyId, values.fiscalYearId, values.fiscalPeriodId, values.from, values.to],
    queryFn: () => getCashFlow({
      fiscalYearId: values.fiscalYearId,
      fiscalPeriodId: values.fiscalPeriodId,
      from: values.from,
      to: values.to,
    }),
    enabled,
  });

  const buckets = report.data?.buckets ?? [];
  const chartData = buckets.map((bucket) => ({
    name: CASH_FLOW_CATEGORY_LABELS[bucket.category],
    ورود: Number(bucket.inflow),
    خروج: Number(bucket.outflow),
    خالص: Number(bucket.net),
  }));

  const summary = report.data ? (
    <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
      <div className="rounded-lg border border-border/60 bg-surface px-3 py-2 text-xs" data-testid="cf-opening">
        <span className="text-muted-foreground">موجودی نقد ابتدای دوره</span>
        <p className="mt-0.5 font-bold tabular-nums">{formatAmount(report.data.openingCash)}</p>
      </div>
      <div className="rounded-lg border border-primary/40 bg-primary/5 px-3 py-2 text-xs" data-testid="cf-closing">
        <span className="text-muted-foreground">موجودی نقد پایان دوره</span>
        <p className="mt-0.5 font-bold tabular-nums">{formatAmount(report.data.closingCash)}</p>
      </div>
      <div className="rounded-lg border border-border/60 bg-surface px-3 py-2 text-xs" data-testid="cf-net-total">
        <span className="text-muted-foreground">جمع خالص جریان‌ها</span>
        <p className="mt-0.5 font-bold tabular-nums">{formatAmount(buckets.reduce((acc, bucket) => acc + Number(bucket.net), 0))}</p>
      </div>
    </div>
  ) : null;

  return (
    <ReportPageShell
      title="جریان وجوه نقد"
      description={`جریان‌های نقد دوره ${report.data ? `${formatToPersianDate(report.data.fromDate)} تا ${formatToPersianDate(report.data.toDate)}` : 'انتخابی'} — روش مستقیم؛ فقط گردش‌های طبقه‌بندی‌شده backend (بدون استنتاج از مانده بانک)`}
      icon={<Banknote className="size-5 text-primary" aria-hidden />}
      filters={{ values: values, companyId, updateFilters, setPage }}
      filterKinds={['year', 'period', 'dates']}
      onRun={() => setEnabled(true)}
      loading={report.isFetching}
      error={report.isError}
      onRetry={() => void report.refetch()}
      generatedAt={report.dataUpdatedAt || undefined}
      summary={summary}
      exportReportName="cashflow"
      exportParams={{ fiscalYearId: values.fiscalYearId, fiscalPeriodId: values.fiscalPeriodId, from: values.from, to: values.to }}
      testIdPrefix="cf"
    >
      {report.data && !report.data.cashAccountsConfigured && (
        <p className="flex items-center gap-2 rounded-md border border-warning/40 bg-warning/5 px-3 py-2 text-xs text-warning" data-testid="cf-unconfigured">
          <AlertTriangle className="size-3.5" aria-hidden />
          حساب‌های وجوه نقد پیکربندی نشده‌اند{report.data.note ? ` — ${report.data.note}` : ''}. گزارش تا پیکربندی، قطعی نیست.
        </p>
      )}
      {report.data?.note && report.data.cashAccountsConfigured && (
        <p className="rounded-md border border-border/60 bg-surface px-3 py-2 text-xs text-muted-foreground" data-testid="cf-note">{report.data.note}</p>
      )}

      {chartData.length > 0 && (
        <Card className="report-no-print">
          <CardHeader className="border-b border-border/60 pb-2.5 text-sm font-bold">نمودار جریان‌ها بر اساس طبقه — مقادیر مستقیم از API</CardHeader>
          <CardContent className="h-56 pt-3" data-testid="cf-chart">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={chartData} margin={{ top: 4, bottom: 4 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#94a3b855" />
                <XAxis dataKey="name" tick={{ fontSize: 10 }} />
                <YAxis tick={{ fontSize: 10 }} width={70} tickFormatter={(value: number) => formatAmount(value)} />
                <ChartTooltip formatter={(value) => formatAmount(Number(value))} contentStyle={{ direction: 'rtl', fontSize: 12 }} />
                <Legend wrapperStyle={{ fontSize: 11 }} />
                <Bar dataKey="ورود" name="ورود نقد" fill="#16a34a" radius={[3, 3, 0, 0]} />
                <Bar dataKey="خروج" name="خروج نقد" fill="#dc2626" radius={[3, 3, 0, 0]} />
                <Bar dataKey="خالص" name="خالص" fill="#2563eb" radius={[3, 3, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader className="border-b border-border/60 pb-2.5 text-sm font-bold">جدول جریان‌های نقد بر اساس طبقه (backend)</CardHeader>
        <CardContent className="p-0">
          {buckets.length === 0 && report.data ? (
            <p className="py-8 text-center text-xs text-muted-foreground">جریانی طبقه‌بندی‌شده در این بازه نیست — طبقه‌بندی حساب‌ها را تکمیل کنید.</p>
          ) : (
            <table className="w-full text-xs" aria-label="جریان وجوه نقد" data-testid="cf-table">
              <thead className="bg-muted/40 text-muted-foreground">
                <tr>
                  <th scope="col" className="px-2.5 py-2 text-start font-semibold">طبقه</th>
                  <th scope="col" className="px-2.5 py-2 text-end font-semibold">ورود نقد</th>
                  <th scope="col" className="px-2.5 py-2 text-end font-semibold">خروج نقد</th>
                  <th scope="col" className="px-2.5 py-2 text-end font-semibold">خالص</th>
                </tr>
              </thead>
              <tbody>
                {buckets.map((bucket) => (
                  <tr key={bucket.category} className="border-t border-border/40 hover:bg-muted/30">
                    <td className="px-2.5 py-2 font-semibold">{CASH_FLOW_CATEGORY_LABELS[bucket.category]}</td>
                    <td className="px-2.5 py-2 text-end tabular-nums text-emerald-700 dark:text-emerald-400">{formatAmount(bucket.inflow)}</td>
                    <td className="px-2.5 py-2 text-end tabular-nums text-danger">{formatAmount(bucket.outflow)}</td>
                    <td className="px-2.5 py-2 text-end"><BalanceCell amount={bucket.net} /></td>
                  </tr>
                ))}
              </tbody>
              <tfoot className="border-t-2 border-border bg-muted/30 font-bold">
                <tr data-testid="cf-totals">
                  <td className="px-2.5 py-2">جمع کل</td>
                  <td className="px-2.5 py-2 text-end tabular-nums">{formatAmount(buckets.reduce((acc, bucket) => acc + Number(bucket.inflow), 0))}</td>
                  <td className="px-2.5 py-2 text-end tabular-nums">{formatAmount(buckets.reduce((acc, bucket) => acc + Number(bucket.outflow), 0))}</td>
                  <td className="px-2.5 py-2 text-end tabular-nums">{formatAmount(buckets.reduce((acc, bucket) => acc + Number(bucket.net), 0))}</td>
                </tr>
              </tfoot>
            </table>
          )}
        </CardContent>
      </Card>

      <ClassificationManager />
    </ReportPageShell>
  );
};

export const CashFlowPage: React.FC = () => (
  <PermissionGuard permissions={[PERMISSIONS.reportsRead]}>
    <CashFlowContent />
  </PermissionGuard>
);
