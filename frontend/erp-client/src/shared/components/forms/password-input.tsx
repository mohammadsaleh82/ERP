import * as React from 'react';
import { Eye, EyeOff } from 'lucide-react';
import { Input } from '@/shared/components/ui/input';
import { IconButton } from '@/shared/components/ui/icon-button';
import { cn } from '@/shared/utils/cn';

export type PasswordInputProps = React.ComponentProps<'input'>;

/** ورودی رمز با دکمه نمایش/مخفی — visibilityToggle بدون ذخیره در state فرم */
export const PasswordInput = React.forwardRef<HTMLInputElement, PasswordInputProps>(
  ({ className, ...props }, ref) => {
    const [visible, setVisible] = React.useState(false);
    return (
      <div className="relative">
        <Input ref={ref} type={visible ? 'text' : 'password'} dir="ltr" className={cn('pl-10', className)} {...props} />
        <IconButton
          type="button"
          aria-label={visible ? 'مخفی کردن رمز' : 'نمایش رمز'}
          variant="ghost"
          size="sm"
          className="absolute top-1/2 left-1 -translate-y-1/2"
          onClick={() => setVisible((v) => !v)}
        >
          {visible ? <EyeOff className="size-4" aria-hidden /> : <Eye className="size-4" aria-hidden />}
        </IconButton>
      </div>
    );
  },
);
PasswordInput.displayName = 'PasswordInput';
