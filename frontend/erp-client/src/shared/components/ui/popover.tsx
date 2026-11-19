import * as React from 'react';
import * as PopoverPrimitive from '@radix-ui/react-popover';
import { cn } from '@/shared/utils/cn';

export const Popover = PopoverPrimitive.Root;
export const PopoverTrigger = PopoverPrimitive.Trigger;

export interface PopoverContentProps extends React.ComponentPropsWithoutRef<typeof PopoverPrimitive.Content> {
  className?: string;
}

/** پوسته استاندارد Popover — RTL، سایه متوسط، بسته‌شدن با Esc/کلیک بیرون */
export const PopoverContent = React.forwardRef<HTMLDivElement, PopoverContentProps>(
  ({ className, align = 'start', sideOffset = 6, ...props }, ref) => (
    <PopoverPrimitive.Portal>
      <PopoverPrimitive.Content
        ref={ref}
        dir="rtl"
        align={align}
        sideOffset={sideOffset}
        className={cn(
          'z-50 rounded-lg border bg-surface p-1.5 shadow-md',
          'data-[state=open]:animate-in data-[state=closed]:animate-out',
          className,
        )}
        {...props}
      />
    </PopoverPrimitive.Portal>
  ),
);
PopoverContent.displayName = 'PopoverContent';
