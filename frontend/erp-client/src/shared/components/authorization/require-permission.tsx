import * as React from 'react';
import { useAuthStore } from '@/core/auth/auth.store';

export interface RequirePermissionProps {
  /** حداقل یکی از مجوزها لازم است */
  permissions: readonly string[];
  children: React.ReactNode;
  /** hide: رندر نشدن | disable: نمایش غیرفعال (پیش‌فرض hide) */
  mode?: 'hide' | 'disable';
  fallback?: React.ReactNode;
}

/**
 * کنترل نمایش دکمه/عملیات بر اساس مجوز واقعی JWT.
 * توجه: این لایه UI است — Backend مرجع نهایی اعمال مجوز (defense-in-depth).
 */
export const RequirePermission: React.FC<RequirePermissionProps> = ({ permissions, children, mode = 'hide', fallback = null }) => {
  const hasAny = useAuthStore((state) => state.hasAnyPermission);
  const allowed = hasAny(permissions);

  if (allowed) return <>{children}</>;
  if (mode === 'disable' && React.isValidElement(children)) {
    return React.cloneElement(children as React.ReactElement<{ disabled?: boolean }>, { disabled: true });
  }
  return <>{fallback}</>;
};
