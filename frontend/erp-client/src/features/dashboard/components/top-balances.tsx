import * as React from 'react';
import { WidgetShell } from '@/shared/components/widget/widget-shell';
import { useDashboardData, type DashboardRange } from '@/features/dashboard/dashboard.queries';
import { formatNumberPersian } from '@/shared/utils/number';

/** مانده حساب‌های منتخب — Top مانده پایانی از داده واقعی /Ledger/balances */
export const TopBalances: React.FC<{ range: DashboardRange }> = ({ range }) => {
  const { balances, canLedger } = useDashboardData(range);

  const top = React.useMemo(() => {
    return [...(balances.data ?? [])]
      .filter((row) => row.closingDisplayBalance !== 0)
      .sort((a, b) => Math.abs(b.closingDisplayBalance) - Math.abs(a.closingDisplayBalance))
      .slice(0, 6);
  }, [balances.data]);

  return (
    <WidgetShell
      title="بزرگ‌ترین مانده حساب‌ها"
      isLoading={balances.isLoading}
      isError={balances.isError}
      isUnauthorized={!canLedger}
      isEmpty={top.length === 0}
      emptyMessage="حسابی با مانده غیرصفر در بازه وجود ندارد"
      onRetry={() => void balances.refetch()}
      action={<span className="text-[11px] text-muted-foreground">بر اساس مانده پایانی</span>}
    >
      <table className="w-full text-xs">
        <thead>
          <tr className="border-b text-muted-foreground">
            <th className="pb-1.5 text-start font-medium">حساب</th>
            <th className="pb-1.5 text-end font-medium">گردش بدهکار</th>
            <th className="pb-1.5 text-end font-medium">گردش بستانکار</th>
            <th className="pb-1.5 text-end font-medium">مانده پایانی</th>
          </tr>
        </thead>
        <tbody>
          {top.map((row) => (
            <tr key={row.accountId} className="border-b border-border/40 last:border-0">
              <td className="min-w-0 max-w-40 truncate py-1.5 pe-2 text-foreground">
                <span dir="ltr" className="block truncate font-code">{row.accountFullCode}</span>
                <span className="block truncate text-[10px] text-muted-foreground">{row.accountName}</span>
              </td>
              <td className="py-1.5 text-end tabular-nums">{formatNumberPersian(row.periodDebit)}</td>
              <td className="py-1.5 text-end tabular-nums">{formatNumberPersian(row.periodCredit)}</td>
              <td className="py-1.5 text-end font-semibold tabular-nums">{formatNumberPersian(row.closingDisplayBalance)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </WidgetShell>
  );
};
