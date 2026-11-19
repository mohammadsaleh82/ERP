import * as React from 'react';
import { ArrowDownLeft, ArrowUpRight, Clock3 } from 'lucide-react';
import { Card, CardContent } from '@/shared/components/ui/card';
import { WidgetShell } from '@/shared/components/widget/widget-shell';
import { useDashboardData, type DashboardRange } from '@/features/dashboard/dashboard.queries';
import { digitsToPersian, formatNumberPersian } from '@/shared/utils/number';

/** چهار کارت خلاصه از داده واقعی: گردش بدهکار/بستانکار، اسناد بازه، در انتظار تأیید */
export const SummaryCards: React.FC<{ range: DashboardRange }> = ({ range }) => {
  const { balances, documents, pending, canLedger, canDocuments } = useDashboardData(range);

  const totalDebit = (balances.data ?? []).reduce((sum, row) => sum + row.periodDebit, 0);
  const totalCredit = (balances.data ?? []).reduce((sum, row) => sum + row.periodCredit, 0);

  const balanced = totalDebit > 0 || totalCredit > 0 ? Math.abs(totalDebit - totalCredit) < 0.01 : true;

  const cards: Array<{
    label: string;
    value: string;
    hint: string;
    icon: React.ElementType;
    loading: boolean;
    unauthorized: boolean;
    isError: boolean;
  }> = [
    {
      label: 'جمع گردش بدهکار',
      value: formatNumberPersian(totalDebit),
      hint: 'از مانده حساب‌های دفتر کل',
      icon: ArrowUpRight,
      loading: balances.isLoading,
      unauthorized: !canLedger,
      isError: balances.isError,
    },
    {
      label: 'جمع گردش بستانکار',
      value: formatNumberPersian(totalCredit),
      hint: balanced ? 'تراز با بدهکار' : 'عدم تراز با بدهکار',
      icon: ArrowDownLeft,
      loading: balances.isLoading,
      unauthorized: !canLedger,
      isError: balances.isError,
    },
    {
      label: 'اسناد بازه جاری',
      value: digitsToPersian(documents.data?.totalCount ?? 0),
      hint: 'تعداد کل اسناد در بازه',
      icon: Clock3,
      loading: documents.isLoading,
      unauthorized: !canDocuments,
      isError: documents.isError,
    },
    {
      label: 'در انتظار تأیید',
      value: digitsToPersian(pending.data?.totalCount ?? 0),
      hint: 'اسناد در جریان تأیید',
      icon: Clock3,
      loading: pending.isLoading,
      unauthorized: !canDocuments,
      isError: pending.isError,
    },
  ];

  return (
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4" data-testid="summary-cards">
      {cards.map((card) => (
        <WidgetShell
          key={card.label}
          title={card.label}
          isLoading={card.loading}
          isUnauthorized={card.unauthorized}
          isError={card.isError}
          onRetry={card.label.startsWith('اسناد') ? (card.hint.includes('جریان') ? pending.refetch : documents.refetch) : balances.refetch}
          errorDetail={undefined}
        >
          <Card className="border-0 shadow-none">
            <CardContent className="flex items-center gap-3 p-1">
              <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-primary-soft text-primary">
                <card.icon className="size-5" aria-hidden />
              </span>
              <span className="min-w-0">
                <span className="block truncate text-lg font-black tabular-nums text-foreground">{card.value}</span>
                <span className="block truncate text-[11px] text-muted-foreground">{card.hint}</span>
              </span>
            </CardContent>
          </Card>
        </WidgetShell>
      ))}
    </div>
  );
};
