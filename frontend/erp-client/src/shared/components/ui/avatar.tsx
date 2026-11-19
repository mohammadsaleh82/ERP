import * as React from 'react';
import { cn } from '@/shared/utils/cn';

export interface AvatarProps extends React.HTMLAttributes<HTMLSpanElement> {
  /** نام کامل؛ حروف اول به‌عنوان fallback تصویر استفاده می‌شود */
  name: string;
  src?: string;
  size?: 'sm' | 'default' | 'lg';
}

const sizeMap = { sm: 'size-7 text-xs', default: 'size-9 text-sm', lg: 'size-11 text-base' } as const;

const initials = (name: string): string =>
  name.trim().split(/\s+/).slice(0, 2).map((part) => part[0] ?? '').join('') || '؟';

/** آواتار کاربر با fallback حروف اول — بدون وابستگی به تصویر خارجی */
export const Avatar = React.forwardRef<HTMLSpanElement, AvatarProps>(
  ({ name, src, size = 'default', className, ...props }, ref) => (
    <span
      ref={ref}
      role="img"
      aria-label={name}
      className={cn(
        'inline-flex shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary-soft font-medium text-primary ring-1 ring-border',
        sizeMap[size],
        className,
      )}
      {...props}
    >
      {src ? <img src={src} alt={name} className="size-full object-cover" /> : initials(name)}
    </span>
  ),
);
Avatar.displayName = 'Avatar';
