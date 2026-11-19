import React from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useCompanyStore } from '@/core/company/company.store';

/** بدون شرکت فعال هیچ داده‌ای درخواست نمی‌شود — هدایت به انتخاب شرکت */
export const CompanyGuard: React.FC = () => {
  const { activeCompany } = useCompanyStore();
  const location = useLocation();

  if (!activeCompany) {
    return <Navigate to="/select-company" state={{ from: location.pathname + location.search }} replace />;
  }

  return <Outlet />;
};
