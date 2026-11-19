import * as React from 'react';
import { useNavigate } from 'react-router-dom';
import { Building2, Check, RefreshCw } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { ErrorState } from '@/shared/components/feedback/error-state';
import { useAuthStore } from '@/core/auth/auth.store';
import { useCompanyStore } from '@/core/company/company.store';
import { getUserCompanies, refreshSession, type UserTenantDto } from '@/core/auth/auth.service';
import { queryClient } from '@/app/providers/query-provider';
import { toast } from '@/core/notifications/toast';
import { cn } from '@/shared/utils/cn';
import { digitsToPersian } from '@/shared/utils/number';

/**
 * انتخاب شرکت واقعی از memberships (GET /Auth/companies).
 * انتخاب → refresh با هدرهای زمینه → access token + permissions شرکت جدید.
 */
export const CompanySelectionPage: React.FC = () => {
  const navigate = useNavigate();
  const setTenants = useCompanyStore((state) => state.setTenants);
  const tenants = useCompanyStore((state) => state.tenants);
  const setCompanyContext = useCompanyStore((state) => state.setCompanyContext);
  const clearAuth = useAuthStore((state) => state.clearAuth);
  const accessToken = useAuthStore((state) => state.accessToken);
  const [dtos, setDtos] = React.useState<UserTenantDto[]>([]);
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState(false);
  const [switching, setSwitching] = React.useState<string | null>(null);

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(false);
    try {
      const companies = await getUserCompanies();
      setDtos(companies);
      setTenants(companies);
    } catch {
      setLoadError(true);
    } finally {
      setLoading(false);
    }
  }, [setTenants]);

  React.useEffect(() => {
    void load();
  }, [load]);

  const handleSelect = async (tenant: UserTenantDto) => {
    const company = tenant.companies[0];
    if (!company) return;
    setSwitching(company.companyId);
    try {
      const session = await refreshSession({ tenantId: tenant.tenantId, companyId: company.companyId });
      useAuthStore.getState().setSession(session.accessToken, session.tenantId, session.companyId, session.permissions);
      setCompanyContext(
        { id: tenant.tenantId, name: tenant.tenantName, companies: tenant.companies.map((c) => ({ id: c.companyId, name: c.name, fiscalYear: c.activeFiscalYear })) },
        { id: company.companyId, name: company.name, fiscalYear: company.activeFiscalYear },
      );
      queryClient.clear();
      toast.success(`شرکت «${company.name}» فعال شد`, 'سال مالی ' + digitsToPersian(company.activeFiscalYear));
      navigate('/', { replace: true });
    } catch {
      toast.error('انتخاب شرکت انجام نشد', 'دسترسی عضویت فعال به این شرکت وجود ندارد.');
    } finally {
      setSwitching(null);
    }
  };

  if (loading) {
    return (
      <div className="w-full space-y-3 rounded-xl border bg-surface p-6 shadow-lg" aria-busy="true">
        <Skeleton className="h-6 w-48" />
        <Skeleton className="h-16 w-full" />
        <Skeleton className="h-16 w-full" />
      </div>
    );
  }

  if (loadError) {
    return (
      <div className="w-full rounded-xl border bg-surface p-4 shadow-lg">
        <ErrorState onRetry={() => void load()} />
      </div>
    );
  }

  return (
    <div className="w-full rounded-xl border bg-surface p-6 shadow-lg">
      <div className="mb-4 flex items-center gap-2">
        <Building2 className="size-5 text-primary" aria-hidden />
        <h1 className="text-base font-bold text-foreground">انتخاب شرکت فعال</h1>
      </div>
      <p className="mb-4 text-sm text-muted-foreground">
        داده‌های هر شرکت جدا نگهداری می‌شود؛ پس از انتخاب، cache داده‌های قبلی پاک‌سازی می‌شود.
      </p>

      <div className="space-y-4">
        {tenants.length === 0 && (
          <div className="rounded-md border bg-muted/40 p-6 text-center">
            <p className="text-sm font-semibold text-foreground">به هیچ شرتی عضو فعال نیستید</p>
            <p className="mt-1 text-xs text-muted-foreground">با مدیر سازمان خود تماس بگیرید.</p>
            <Button variant="outline" size="sm" className="mt-3" onClick={() => { clearAuth(); navigate('/login', { replace: true }); }}>
              خروج
            </Button>
          </div>
        )}

        {dtos.map((tenant) => (
          <section key={tenant.tenantId} className="rounded-lg border p-3">
            <p className="mb-2 text-xs font-semibold text-muted-foreground">{tenant.tenantName}</p>
            <div className="space-y-2">
              {tenant.companies.map((company) => {
                const selected = switching === company.companyId;
                return (
                  <button
                    key={company.companyId}
                    type="button"
                    disabled={switching !== null || !company.isActive}
                    onClick={() => void handleSelect(tenant)}
                    className={cn(
                      'flex w-full cursor-pointer items-center justify-between gap-3 rounded-md border px-3.5 py-3 text-start transition-colors',
                      'hover:border-primary hover:bg-primary-soft/60',
                      'disabled:cursor-not-allowed disabled:opacity-50',
                      selected && 'border-primary bg-primary-soft',
                    )}
                  >
                    <span className="flex min-w-0 flex-col">
                      <span className="truncate text-sm font-semibold text-foreground">{company.name}</span>
                      <span className="text-[11px] text-muted-foreground">
                        سال مالی {digitsToPersian(company.activeFiscalYear)}
                        {!company.isActive && ' · غیرفعال'}
                      </span>
                    </span>
                    <span className="flex items-center gap-2 text-primary">
                      {selected ? (
                        <RefreshCw className="size-4 animate-spin" aria-hidden />
                      ) : (
                        <Check className="size-4 opacity-0 transition-opacity peer-hover:opacity-100" aria-hidden />
                      )}
                    </span>
                  </button>
                );
              })}
            </div>
          </section>
        ))}
      </div>

      <div className="mt-5 flex justify-start">
        <Button
          variant="ghost"
          size="sm"
          onClick={() => {
            if (accessToken) navigate('/');
          }}
        >
          ادامه بدون انتخاب (نمایش عمومی)
        </Button>
      </div>
    </div>
  );
};
