import * as React from 'react';
import { Badge } from '@/shared/components/ui/badge';
import { voucherStatusLabel, type VoucherStatus } from '@/shared/utils/status';

const toneToVariant = {
  neutral: 'secondary',
  warning: 'warning',
  success: 'success',
  info: 'info',
  primary: 'default',
} as const;

export interface VoucherStatusBadgeProps {
  status: string;
  className?: string;
}

/** Badge وضعیت سند — منبع رنگ از status.ts */
export const VoucherStatusBadge: React.FC<VoucherStatusBadgeProps> = ({ status, className }) => {
  const descriptor = voucherStatusLabel(status);
  return (
    <Badge
      variant={toneToVariant[descriptor.tone]}
      className={className}
    >
      {descriptor.label}
    </Badge>
  );
};

export type { VoucherStatus };
