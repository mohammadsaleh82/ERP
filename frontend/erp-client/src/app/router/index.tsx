import { createBrowserRouter } from 'react-router-dom';
import { AppLayout } from '@/app/layouts/app-layout';
import { AuthLayout } from '@/app/layouts/auth-layout';
import { AuthGuard } from '@/app/guards/auth-guard';
import { CompanyGuard } from '@/app/guards/company-guard';
import { PermissionGuard } from '@/app/guards/permission-guard';
import { PERMISSIONS } from '@/core/auth/permissions';

import { LoginPage } from '@/features/identity/pages/login-page';
import { NotFoundPage } from '@/features/identity/pages/not-found-page';
import { AccessDeniedPage } from '@/features/identity/pages/access-denied-page';
import { CompanySelectionPage } from '@/features/organization/pages/company-selection-page';
import { DashboardPage } from '@/features/dashboard/pages/dashboard-page';

import { AccountingPage } from '@/features/accounting';
import { TreasuryPage } from '@/features/treasury';
import { InventoryPage } from '@/features/inventory';
import { SalesPage } from '@/features/sales';
import { PurchasingPage } from '@/features/purchasing';
import { PartiesPage } from '@/features/parties';
import { PayrollPage } from '@/features/payroll';
import { ReportingPage } from '@/features/reporting';
import { OrganizationPage } from '@/features/organization';
import { SettingsPage } from '@/features/settings';
import { ProfilePage } from '@/features/settings/pages/profile-page';
import { DesignSystemShowcasePage } from '@/features/design-system/pages/design-system-showcase-page';

export const router = createBrowserRouter([
  // Public Auth Routes
  {
    element: <AuthLayout />,
    children: [
      {
        path: '/login',
        element: <LoginPage />,
      },
    ],
  },

  // Authenticated Pre-Company Route
  {
    element: <AuthGuard />,
    children: [
      {
        path: '/select-company',
        element: <CompanySelectionPage />,
      },
      {
        path: '/access-denied',
        element: <AccessDeniedPage />,
      },
    ],
  },

  // Protected Routes within Active Company Context
  {
    element: <AuthGuard />,
    children: [
      {
        element: <CompanyGuard />,
        children: [
          {
            element: <AppLayout />,
            children: [
              {
                path: '/',
                element: <DashboardPage />,
              },
              {
                path: '/accounting',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.documentsRead, PERMISSIONS.accountsRead]}>
                    <AccountingPage />
                  </PermissionGuard>
                ),
              },
              {
                path: '/treasury',
                element: <TreasuryPage />,
              },
              {
                path: '/inventory',
                element: <InventoryPage />,
              },
              {
                path: '/sales',
                element: <SalesPage />,
              },
              {
                path: '/purchasing',
                element: <PurchasingPage />,
              },
              {
                path: '/parties',
                element: <PartiesPage />,
              },
              {
                path: '/payroll',
                element: <PayrollPage />,
              },
              {
                path: '/reporting',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.reportsRead, PERMISSIONS.ledgerRead]}>
                    <ReportingPage />
                  </PermissionGuard>
                ),
              },
              {
                path: '/organization',
                element: <OrganizationPage />,
              },
              {
                path: '/identity',
                element: <OrganizationPage />, // Reusable admin view for skeleton
              },
              {
                path: '/settings',
                element: <SettingsPage />,
              },
              {
                path: '/profile',
                element: <ProfilePage />,
              },
              {
                path: '/design-system',
                element: <DesignSystemShowcasePage />,
              },
            ],
          },
        ],
      },
    ],
  },

  // 404 Catch-All
  {
    path: '*',
    element: <NotFoundPage />,
  },
]);
