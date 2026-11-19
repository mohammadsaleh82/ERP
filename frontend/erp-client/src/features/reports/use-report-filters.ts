/**
 * فیلترهای گزارش در querystring — قابل اشتراک‌گذاری و بازگشت.
 * مقادیر از URL خوانده و با updateParams یکجا نوشته می‌شوند (بدون stale closure).
 * companyId در کلید query ها لحاظ می‌شود تا cache شرکت قبلی هرگز نمایش داده نشود.
 */
import * as React from 'react';
import { useSearchParams } from 'react-router-dom';
import { useCompanyStore } from '@/core/company/company.store';

export interface ReportUrlFilters {
  values: {
    fiscalYearId?: string;
    fiscalPeriodId?: string;
    from?: string;
    to?: string;
    accountId?: string;
    accountFullCode?: string;
    accountName?: string;
    parentAccountId?: string;
    parentAccountFullCode?: string;
    parentAccountName?: string;
    detailId?: string;
    costCenterId?: string;
    projectId?: string;
    partyId?: string;
    currencyCode?: string;
    accountLevel?: string;
    page?: number;
  };
  companyId: string | null;
  updateFilters: (updates: Record<string, string | undefined>) => void;
  setPage: (page: number) => void;
}

const PARAM_KEYS = ['fiscalYearId', 'fiscalPeriodId', 'from', 'to', 'accountId', 'accountFullCode', 'accountName', 'parentAccountId', 'parentAccountFullCode', 'parentAccountName', 'detailId', 'costCenterId', 'projectId', 'partyId', 'currencyCode', 'accountLevel'] as const;

export function useReportFilters(): ReportUrlFilters {
  const [params, setParams] = useSearchParams();
  const companyId = useCompanyStore((state) => state.activeCompany?.id ?? null);

  const values = React.useMemo(() => {
    const result: Record<string, string | undefined | number> = {};
    for (const key of PARAM_KEYS) result[key] = params.get(key) ?? undefined;
    result.page = Number(params.get('page') ?? '1') || 1;
    return result;
  }, [params]);

  const updateFilters = React.useCallback(
    (updates: Record<string, string | undefined>) => {
      const next = new URLSearchParams(params);
      for (const [key, value] of Object.entries(updates)) {
        if (value) next.set(key, value);
        else next.delete(key);
      }
      // هر تغییر فیلتر → بازگشت به صفحه اول
      next.delete('page');
      setParams(next, { replace: true });
    },
    [params, setParams],
  );

  const setPage = React.useCallback(
    (nextPage: number) => {
      const next = new URLSearchParams(params);
      next.set('page', String(Math.max(1, nextPage)));
      setParams(next, { replace: true });
    },
    [params, setParams],
  );

  return { values: values as ReportUrlFilters['values'], companyId, updateFilters, setPage };
}
