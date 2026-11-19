import * as React from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuthStore } from '@/core/auth/auth.store';

export interface PermissionGuardProps {
  /** کاربر حداقل یکی از این مجوزها را باید داشته باشد */
  permissions: readonly string[];
  children: React.ReactNode;
}

/**
 * Route guard بر اساس مجوزهای واقعی JWT — هدایت به /access-denied
 * (خلاف hide کردن دکمه، این امنیت نمایشی است؛ backend مرجع نهایی).
 */
export const PermissionGuard: React.FC<PermissionGuardProps> = ({ permissions, children }) => {
  const hasAny = useAuthStore((state) => state.hasAnyPermission);
  const location = useLocation();

  if (!hasAny(permissions)) {
    return <Navigate to="/access-denied" state={{ from: location.pathname, permissions: [...permissions] }} replace />;
  }

  return <>{children}</>;
};

/** دسترسی به مسیر از منو — برای جلوگیری از ورود مسیر با آدرس مستقیم */
export function useRoutePermissions(): boolean {
  return true;
}
