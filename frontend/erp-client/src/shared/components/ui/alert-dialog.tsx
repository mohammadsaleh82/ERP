import * as React from 'react';
import * as AlertDialogPrimitive from '@radix-ui/react-alert-dialog';
import { Button, type ButtonProps } from '@/shared/components/ui/button';

export interface AlertDialogActionProps extends ButtonProps {
  onSelect: () => void;
}

/** پنجره تأیید خطرناک — فکوس روی دکمه لغو، اکشن به‌عنوان variant destructive */
export const AlertDialog: React.FC<{
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description: React.ReactNode;
  actionLabel: string;
  cancelLabel?: string;
  onAction: () => void;
}> = ({ open, onOpenChange, title, description, actionLabel, cancelLabel = 'انصراف', onAction }) => (
  <AlertDialogPrimitive.Root open={open} onOpenChange={onOpenChange}>
    <AlertDialogPrimitive.Portal>
      <AlertDialogPrimitive.Overlay className="fixed inset-0 z-50 bg-foreground/40 backdrop-blur-[2px]" />
      <AlertDialogPrimitive.Content
        dir="rtl"
        className="fixed top-1/2 left-1/2 z-50 w-[calc(100vw-2rem)] max-w-md -translate-x-1/2 -translate-y-1/2 rounded-xl border bg-surface p-5 shadow-lg focus:outline-none"
      >
        <AlertDialogPrimitive.Title className="text-base font-bold text-foreground">{title}</AlertDialogPrimitive.Title>
        <AlertDialogPrimitive.Description className="mt-2 text-sm leading-6 text-muted-foreground">
          {description}
        </AlertDialogPrimitive.Description>
        <div className="mt-5 flex items-center justify-start gap-2">
          <AlertDialogPrimitive.Cancel asChild>
            <Button variant="outline">{cancelLabel}</Button>
          </AlertDialogPrimitive.Cancel>
          <AlertDialogPrimitive.Action asChild>
            <Button variant="destructive" onClick={onAction}>
              {actionLabel}
            </Button>
          </AlertDialogPrimitive.Action>
        </div>
      </AlertDialogPrimitive.Content>
    </AlertDialogPrimitive.Portal>
  </AlertDialogPrimitive.Root>
);
