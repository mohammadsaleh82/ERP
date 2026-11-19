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
import { AccountsPage } from '@/features/accounts';
import { FiscalYearsPage } from '@/features/fiscal';
import { AccountingSettingsPage } from '@/features/accounting/pages/accounting-settings-page';
import { VouchersPage } from '@/features/documents';
import { VoucherFormPage } from '@/features/documents/pages/voucher-form-page';
import { VoucherDetailPage } from '@/features/documents/pages/voucher-detail-page';
import {
  JournalPage,
  GeneralLedgerPage,
  LedgerTransactionsPage,
  SubsidiaryLedgerPage,
  AccountTurnoverPage,
  AccountCardPage,
  AccountBalancesPage,
  TrialBalancePage,
  DimensionsTurnoverPage,
  CombinedDimensionsPage,
} from '@/features/reports';
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
                path: '/accounting/documents',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.documentsRead]}>
                    <VouchersPage />
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/documents/new',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.documentsCreate]}>
                    <VoucherFormPage />
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/documents/:id',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.documentsRead]}>
                    <VoucherDetailPage />
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/reports/journal',
                element: <JournalPage />,
              },
              {
                path: '/accounting/reports/general-ledger',
                element: <GeneralLedgerPage />,
              },
              {
                path: '/accounting/reports/general-ledger/:accountId',
                element: <LedgerTransactionsPage />,
              },
              {
                path: '/accounting/reports/subsidiary',
                element: <SubsidiaryLedgerPage />,
              },
              {
                path: '/accounting/reports/turnover',
                element: <AccountTurnoverPage />,
              },
              {
                path: '/accounting/reports/account-card',
                element: <AccountCardPage />,
              },
              {
                path: '/accounting/reports/balances',
                element: <AccountBalancesPage />,
              },
              {
                path: '/accounting/reports/trial-balance',
                element: <TrialBalancePage />,
              },
              {
                path: '/accounting/reports/dimensions',
                element: <DimensionsTurnoverPage />,
              },
              {
                path: '/accounting/reports/dimensions/combined',
                element: <CombinedDimensionsPage />,
              },
              {
                path: '/accounting/accounts',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.accountsRead]}>
                    <AccountsPage />
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/fiscal-years',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.fiscalYearsRead]}>
                    <FiscalYearsPage />
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/settings',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.settingsRead]}>
                    <AccountingSettingsPage />
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
