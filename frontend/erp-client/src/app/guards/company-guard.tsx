import React from 'react';
import { Navigate, Outlet } from 'react-router-dom';
import { useCompanyStore } from '@/core/company/company.store';

export const CompanyGuard: React.FC = () => {
  const { activeCompany } = useCompanyStore();

  if (!activeCompany) {
    return <Navigate to="/select-company" replace />;
  }

  return <Outlet />;
};
