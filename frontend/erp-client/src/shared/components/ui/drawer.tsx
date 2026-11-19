import * as React from 'react';
import * as DialogPrimitive from '@radix-ui/react-dialog';
import { X } from 'lucide-react';
import { cn } from '@/shared/utils/cn';
import { IconButton } from '@/shared/components/ui/icon-button';

export interface DrawerProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: string;
  children: React.ReactNode;
  /** سمت بازشدن — پیش‌فرض چپ (پایان RTL) */
  side?: 'left' | 'right';
  widthClass?: string;
}

/** پنل کشویی کنار صفحه — جزئیات سند/لیست بلند؛ در موبایل جایگزین مودال */
export const Drawer: React.FC<DrawerProps> = ({
  open,
  onOpenChange,
  title,
  description,
  children,
  side = 'left',
  widthClass = 'w-[85vw] max-w-md',
}) => (
  <DialogPrimitive.Root open={open} onOpenChange={onOpenChange}>
    <DialogPrimitive.Portal>
      <DialogPrimitive.Overlay className="fixed inset-0 z-50 bg-foreground/40 backdrop-blur-[2px]" />
      <DialogPrimitive.Content
        dir="rtl"
        className={cn(
          'fixed top-0 bottom-0 z-50 flex flex-col bg-surface p-5 shadow-lg focus:outline-none',
          widthClass,
          side === 'left' ? 'left-0 border-r' : 'right-0 border-l',
        )}
      >
        <div className="mb-4 flex items-start justify-between gap-4 border-b pb-3">
          <div>
            <DialogPrimitive.Title className="text-base font-bold text-foreground">{title}</DialogPrimitive.Title>
            {description && (
              <DialogPrimitive.Description className="mt-1 text-sm text-muted-foreground">
                {description}
              </DialogPrimitive.Description>
            )}
          </div>
          <DialogPrimitive.Close asChild>
            <IconButton aria-label="بستن" variant="ghost" size="sm">
              <X className="size-4" aria-hidden />
            </IconButton>
          </DialogPrimitive.Close>
        </div>
        <div className="thin-scroll flex-1 overflow-y-auto">{children}</div>
      </DialogPrimitive.Content>
    </DialogPrimitive.Portal>
  </DialogPrimitive.Root>
);
