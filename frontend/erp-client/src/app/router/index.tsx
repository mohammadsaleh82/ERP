import React, { Suspense } from 'react';
import { createBrowserRouter } from 'react-router-dom';
import { AppLayout } from '@/app/layouts/app-layout';
import { AuthLayout } from '@/app/layouts/auth-layout';
import { AuthGuard } from '@/app/guards/auth-guard';
import { CompanyGuard } from '@/app/guards/company-guard';
import { PermissionGuard } from '@/app/guards/permission-guard';
import { PERMISSIONS } from '@/core/auth/permissions';
import { LoadingState } from '@/shared/components/feedback/loading-state';
import { NotFoundPage } from '@/features/identity/pages/not-found-page';
import { AccessDeniedPage } from '@/features/identity/pages/access-denied-page';

/* فاز ۹ — code splitting: هر صفحه chunk خودش؛ fallback مشترک LoadingState.
   صفحات کوچک مشترک (404/deny) eager می‌مانند. helper با named exports کار می‌کند. */
const PartyLedgerWrapper: React.FC<{ kind: 'receivables' | 'payables' }> = ({ kind }) => {
  const Component = React.lazy(() =>
    import('@/features/reports/statements-index').then((mod) => ({ default: mod.PartyLedgerPage })),
  );
  return (
    <Suspense fallback={<LoadingState message="در حال بارگذاری صفحه…" />}>
      <Component kind={kind} />
    </Suspense>
  );
};

const page = (factory: () => Promise<Record<string, unknown>>, name: string) => {
  const Component = React.lazy(async () => {
    const mod = (await factory()) as Record<string, React.ComponentType>;
    return { default: mod[name] };
  });
  return (
    <Suspense fallback={<LoadingState message="در حال بارگذاری صفحه…" />}>
      <Component />
    </Suspense>
  );
};

export const router = createBrowserRouter([
  // Public Auth Routes
  {
    element: <AuthLayout />,
    children: [
      {
        path: '/login',
        element: page(() => import('@/features/identity/pages/login-page'), 'LoginPage'),
      },
    ],
  },

  // Authenticated Pre-Company Route
  {
    element: <AuthGuard />,
    children: [
      {
        path: '/select-company',
        element: page(() => import('@/features/organization/pages/company-selection-page'), 'CompanySelectionPage'),
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
                element: page(() => import('@/features/dashboard/pages/dashboard-page'), 'DashboardPage'),
              },
              {
                path: '/accounting',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.documentsRead, PERMISSIONS.accountsRead]}>
                    {page(() => import('@/features/accounting'), 'AccountingPage')}
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/documents',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.documentsRead]}>
                    {page(() => import('@/features/documents'), 'VouchersPage')}
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/documents/new',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.documentsCreate]}>
                    {page(() => import('@/features/documents/pages/voucher-form-page'), 'VoucherFormPage')}
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/documents/:id',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.documentsRead]}>
                    {page(() => import('@/features/documents/pages/voucher-detail-page'), 'VoucherDetailPage')}
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/reports/journal',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.reportsRead, PERMISSIONS.ledgerRead]}>
                    {page(() => import('@/features/reports'), 'JournalPage')}
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/reports/general-ledger',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.reportsRead, PERMISSIONS.ledgerRead]}>
                    {page(() => import('@/features/reports'), 'GeneralLedgerPage')}
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/reports/general-ledger/:accountId',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.reportsRead, PERMISSIONS.ledgerRead]}>
                    {page(() => import('@/features/reports'), 'LedgerTransactionsPage')}
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/reports/subsidiary',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.reportsRead, PERMISSIONS.ledgerRead]}>
                    {page(() => import('@/features/reports'), 'SubsidiaryLedgerPage')}
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/reports/turnover',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.reportsRead, PERMISSIONS.ledgerRead]}>
                    {page(() => import('@/features/reports'), 'AccountTurnoverPage')}
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/reports/account-card',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.reportsRead, PERMISSIONS.ledgerRead]}>
                    {page(() => import('@/features/reports'), 'AccountCardPage')}
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/reports/balances',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.reportsRead, PERMISSIONS.ledgerRead]}>
                    {page(() => import('@/features/reports'), 'AccountBalancesPage')}
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/reports/trial-balance',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.reportsRead, PERMISSIONS.ledgerRead]}>
                    {page(() => import('@/features/reports'), 'TrialBalancePage')}
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/reports/dimensions',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.reportsRead, PERMISSIONS.ledgerRead]}>
                    {page(() => import('@/features/reports'), 'DimensionsTurnoverPage')}
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/reports/dimensions/combined',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.reportsRead, PERMISSIONS.ledgerRead]}>
                    {page(() => import('@/features/reports'), 'CombinedDimensionsPage')}
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/reports/balance-sheet',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.reportsRead, PERMISSIONS.ledgerRead]}>
                    {page(() => import('@/features/reports/statements-index'), 'BalanceSheetPage')}
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/reports/profit-loss',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.reportsRead, PERMISSIONS.ledgerRead]}>
                    {page(() => import('@/features/reports/statements-index'), 'ProfitLossPage')}
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/reports/cash-flow',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.reportsRead, PERMISSIONS.ledgerRead]}>
                    {page(() => import('@/features/reports/statements-index'), 'CashFlowPage')}
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/reports/receivables',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.reportsRead, PERMISSIONS.ledgerRead]}>
                    <PartyLedgerWrapper kind="receivables" />
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/reports/payables',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.reportsRead, PERMISSIONS.ledgerRead]}>
                    <PartyLedgerWrapper kind="payables" />
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/reports/aging',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.reportsRead, PERMISSIONS.ledgerRead]}>
                    {page(() => import('@/features/reports/statements-index'), 'AgingPage')}
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/accounts',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.accountsRead]}>
                    {page(() => import('@/features/accounts'), 'AccountsPage')}
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/fiscal-years',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.fiscalYearsRead]}>
                    {page(() => import('@/features/fiscal'), 'FiscalYearsPage')}
                  </PermissionGuard>
                ),
              },
              {
                path: '/accounting/settings',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.settingsRead]}>
                    {page(() => import('@/features/accounting/pages/accounting-settings-page'), 'AccountingSettingsPage')}
                  </PermissionGuard>
                ),
              },
              {
                path: '/treasury',
                element: page(() => import('@/features/treasury'), 'TreasuryPage'),
              },
              {
                path: '/inventory',
                element: page(() => import('@/features/inventory'), 'InventoryPage'),
              },
              {
                path: '/sales',
                element: page(() => import('@/features/sales'), 'SalesPage'),
              },
              {
                path: '/purchasing',
                element: page(() => import('@/features/purchasing'), 'PurchasingPage'),
              },
              {
                path: '/parties',
                element: page(() => import('@/features/parties'), 'PartiesPage'),
              },
              {
                path: '/payroll',
                element: page(() => import('@/features/payroll'), 'PayrollPage'),
              },
              {
                path: '/reporting',
                element: (
                  <PermissionGuard permissions={[PERMISSIONS.reportsRead, PERMISSIONS.ledgerRead]}>
                    {page(() => import('@/features/reporting'), 'ReportingPage')}
                  </PermissionGuard>
                ),
              },
              {
                path: '/organization',
                element: page(() => import('@/features/organization'), 'OrganizationPage'),
              },
              {
                path: '/identity',
                element: page(() => import('@/features/organization'), 'OrganizationPage'), // Reusable admin view for skeleton
              },
              {
                path: '/settings',
                element: page(() => import('@/features/settings'), 'SettingsPage'),
              },
              {
                path: '/profile',
                element: page(() => import('@/features/settings/pages/profile-page'), 'ProfilePage'),
              },
              {
                path: '/design-system',
                element: page(() => import('@/features/design-system/pages/design-system-showcase-page'), 'DesignSystemShowcasePage'),
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
