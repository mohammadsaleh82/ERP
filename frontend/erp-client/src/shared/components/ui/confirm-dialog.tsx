import * as React from 'react';
import { AlertDialog } from '@/shared/components/ui/alert-dialog';

export interface ConfirmDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description: React.ReactNode;
  actionLabel: string;
  /** متن دکمه انصراف — پیش‌فرض «انصراف» */
  cancelLabel?: string;
  onConfirm: () => void;
}

/** Wrapper سبک AlertDialog برای تأیید عملیات‌های مهم (حذف، بستن دوره و…) */
export const ConfirmDialog: React.FC<ConfirmDialogProps> = ({
  open,
  onOpenChange,
  title,
  description,
  actionLabel,
  cancelLabel,
  onConfirm,
}) => (
  <AlertDialog
    open={open}
    onOpenChange={onOpenChange}
    title={title}
    description={description}
    actionLabel={actionLabel}
    cancelLabel={cancelLabel}
    onAction={onConfirm}
  />
);
