import * as React from 'react';
import * as TooltipPrimitive from '@radix-ui/react-tooltip';
import { cn } from '@/shared/utils/cn';

interface TooltipProps {
  /** متن راهنما */
  content: React.ReactNode;
  children: React.ReactNode;
  side?: 'top' | 'bottom' | 'left' | 'right';
  className?: string;
}

/** راهنمای کوتاه با تأخیر کوتاه — برای آیکون‌ها و اکشن‌های کم‌نمایش */
export const Tooltip: React.FC<TooltipProps> = ({ content, children, side = 'top', className }) => (
  <TooltipPrimitive.Provider delayDuration={300}>
    <TooltipPrimitive.Root>
      <TooltipPrimitive.Trigger asChild>{children}</TooltipPrimitive.Trigger>
      <TooltipPrimitive.Portal>
        <TooltipPrimitive.Content
          side={side}
          dir="rtl"
          className={cn(
            'z-50 rounded-md bg-foreground px-2.5 py-1.5 text-xs text-background shadow-md',
            'data-[state=delayed-open]:animate-in data-[state=closed]:animate-out',
            className,
          )}
        >
          {content}
        </TooltipPrimitive.Content>
      </TooltipPrimitive.Portal>
    </TooltipPrimitive.Root>
  </TooltipPrimitive.Provider>
);
