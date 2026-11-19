/* eslint-disable react-refresh/only-export-components */
import * as React from 'react';
import { Slot } from '@radix-ui/react-slot';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/shared/utils/cn';

const iconButtonVariants = cva(
  'inline-flex items-center justify-center rounded-md transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 select-none cursor-pointer',
  {
    variants: {
      variant: {
        default: 'bg-primary text-primary-foreground hover:bg-primary/90 shadow-xs',
        destructive: 'bg-danger text-danger-foreground hover:bg-danger/90 shadow-xs',
        outline: 'border border-input bg-background hover:bg-muted',
        ghost: 'hover:bg-muted hover:text-foreground',
      },
      size: {
        sm: 'size-8',
        default: 'size-9',
        lg: 'size-10',
      },
    },
    defaultVariants: { variant: 'ghost', size: 'default' },
  },
);

export interface IconButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof iconButtonVariants> {
  asChild?: boolean;
  /** برچسب دسترس‌پذیری — الزامی چون فقط آیکون دارد */
  'aria-label': string;
}

/** دکمه فقط-آیکون؛ aria-label الزامی برای دسترس‌پذیری */
export const IconButton = React.forwardRef<HTMLButtonElement, IconButtonProps>(
  ({ className, variant, size, asChild = false, ...props }, ref) => {
    const Comp = asChild ? Slot : 'button';
    return <Comp ref={ref} className={cn(iconButtonVariants({ variant, size }), className)} {...props} />;
  },
);
IconButton.displayName = 'IconButton';

export { iconButtonVariants };
