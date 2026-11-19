/**
 * Queryهای داشبورد — همه کلیدها به شرکت فعال scoped هستند
 * تا بعد از تعویض شرکت (queryClient.clear) هیچ داده شرکت قبلی باقی نماند.
 */
import { useQuery } from '@tanstack/react-query';
import { useCompanyStore } from '@/core/company/company.store';
import { useAuthStore } from '@/core/auth/auth.store';
import { PERMISSIONS } from '@/core/auth/permissions';
import * as dashboardApi from '@/core/dashboard/dashboard.service';
import type {
  AccountBalanceResult,
  AuditTrailEntryDto,
  FiscalPeriodDto,
  FiscalYearDto,
  PagedDocumentsResult,
} from '@/core/dashboard/dashboard.types';

export interface DashboardRange {
  from?: string;
  to?: string;
}

export function useDashboardData(range: DashboardRange) {
  const companyId = useCompanyStore((state) => state.activeCompany?.id);
  const has = useAuthStore((state) => state.hasPermission);

  const canLedger = has(PERMISSIONS.ledgerRead);
  const canDocuments = has(PERMISSIONS.documentsRead);
  const canFiscal = has(PERMISSIONS.fiscalYearsRead);
  const canAudit = has(PERMISSIONS.auditRead);

  const balances = useQuery<AccountBalanceResult[]>({
    queryKey: ['dashboard', companyId, 'balances', range.from, range.to],
    queryFn: () => dashboardApi.getLedgerBalances({ from: range.from, to: range.to }),
    enabled: Boolean(companyId) && canLedger,
    staleTime: 1000 * 60,
  });

  const documents = useQuery<PagedDocumentsResult>({
    queryKey: ['dashboard', companyId, 'documents-range', range.from, range.to],
    queryFn: () => dashboardApi.searchDocuments({ fromDate: range.from, toDate: range.to, pageSize: 100 }),
    enabled: Boolean(companyId) && canDocuments,
    staleTime: 1000 * 60,
  });

  const pending = useQuery<PagedDocumentsResult>({
    queryKey: ['dashboard', companyId, 'documents-pending'],
    queryFn: () => dashboardApi.searchDocuments({ status: 'PendingApproval', pageSize: 5 }),
    enabled: Boolean(companyId) && canDocuments,
    staleTime: 1000 * 60,
  });

  const fiscalYears = useQuery<FiscalYearDto[]>({
    queryKey: ['dashboard', companyId, 'fiscal-years'],
    queryFn: () => dashboardApi.getFiscalYears(),
    enabled: Boolean(companyId) && canFiscal,
    staleTime: 1000 * 60 * 5,
  });

  const openYear = fiscalYears.data?.find((year) => year.status === 'Open');

  const periods = useQuery<FiscalPeriodDto[]>({
    queryKey: ['dashboard', companyId, 'periods', openYear?.id],
    queryFn: () => dashboardApi.getFiscalPeriods(openYear!.id),
    enabled: Boolean(companyId) && canFiscal && Boolean(openYear?.id),
    staleTime: 1000 * 60 * 5,
  });

  const audit = useQuery<AuditTrailEntryDto[]>({
    queryKey: ['dashboard', companyId, 'audit-recent'],
    queryFn: () => dashboardApi.getRecentAuditTrail(8),
    enabled: Boolean(companyId) && canAudit,
    staleTime: 1000 * 30,
  });

  return { companyId, canLedger, canDocuments, canFiscal, canAudit, balances, documents, pending, fiscalYears, periods, audit };
}
