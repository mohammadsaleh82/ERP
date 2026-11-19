import * as React from 'react';
import { Hash, CalendarDays } from 'lucide-react';
import { Card, CardContent } from '@/shared/components/ui/card';
import { VoucherStatusBadge } from '@/shared/components/financial/voucher-status-badge';
import { MoneyDisplay } from '@/shared/components/financial/money-display';
import { formatDualDate } from '@/shared/utils/date';

export interface VoucherInfo {
  /** شماره سند */
  number: string;
  date: string;
  status: string;
  /** توضیح سند */
  description?: string;
  /** جمع کل سند */
  totalAmount: number;
}

export interface VoucherInfoCardProps {
  voucher: VoucherInfo;
  /** تراکم نمایش — compact برای لیست‌ها */
  compact?: boolean;
  className?: string;
}

/** کارت خلاصه اطلاعات سند حسابداری — شماره LTR، تاریخ دوگانه، وضعیت رنگی */
export const VoucherInfoCard: React.FC<VoucherInfoCardProps> = ({ voucher, compact = false, className }) => (
  <Card className={className}>
    <CardContent className={compact ? 'flex flex-wrap items-center gap-x-6 gap-y-2 p-4' : 'flex flex-wrap items-center gap-x-6 gap-y-2 p-5'}>
      <span className="flex items-center gap-1.5 text-sm">
        <Hash className="size-4 text-muted-foreground" aria-hidden />
        <span className="font-code font-bold">{voucher.number}</span>
      </span>
      <span className="flex items-center gap-1.5 text-sm text-muted-foreground">
        <CalendarDays className="size-4" aria-hidden />
        {formatDualDate(voucher.date)}
      </span>
      <VoucherStatusBadge status={voucher.status} />
      {voucher.description && <span className="bidi-safe min-w-0 flex-1 truncate text-sm text-muted-foreground">{voucher.description}</span>}
      <span className="flex items-center gap-1.5 text-sm">
        <span className="text-muted-foreground">جمع:</span>
        <MoneyDisplay amount={voucher.totalAmount} options={{ withCurrency: true }} />
      </span>
    </CardContent>
  </Card>
);
