import React from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuthStore } from '@/core/auth/auth.store';

/**
 * Route محافظت‌شده: بدون نشست معتبر → /login با redirect state.
 * بازگشت پس از login به همان مسیر انجام می‌شود.
 */
export const AuthGuard: React.FC = () => {
  const { isAuthenticated } = useAuthStore();
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location.pathname + location.search }} replace />;
  }

  return <Outlet />;
};
