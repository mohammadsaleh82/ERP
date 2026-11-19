import * as React from 'react';
import { useNavigate } from 'react-router-dom';
import { Bell, Building, CalendarRange, Check, ChevronDown, LogOut, Moon, Sun, UserRound } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { Avatar } from '@/shared/components/ui/avatar';
import { Popover, PopoverContent, PopoverTrigger } from '@/shared/components/ui/popover';
import { Tooltip } from '@/shared/components/ui/tooltip';
import { toast } from '@/core/notifications/toast';
import { useCompanyStore, type CompanySummary, type TenantSummary } from '@/core/company/company.store';
import { useAuthStore } from '@/core/auth/auth.store';
import { queryClient } from '@/app/providers/query-provider';
import { logout, refreshSession } from '@/core/auth/auth.service';
import { cn } from '@/shared/utils/cn';
import { digitsToPersian } from '@/shared/utils/number';

/**
 * Header حرفه‌ای: شرکت فعال (Popover با شرکت‌های واقعی کاربر)،
 * سال مالی/دوره فعال، تم، اعلان، منوی کاربری با خروج امن.
 */
export const Header: React.FC = () => {
  const navigate = useNavigate();
  const { activeTenant, activeCompany, tenants, theme, toggleTheme } = useCompanyStore();
  const { user } = useAuthStore();
  const [switching, setSwitching] = React.useState<string | null>(null);
  const [companyOpen, setCompanyOpen] = React.useState(false);
  const [userOpen, setUserOpen] = React.useState(false);

  /**
   * تعویض شرکت: refresh با هدرهای زمینه جدید → access token + permissions جدید
   * → پاک‌سازی کامل query cache تا داده شرکت قبلی نشت نکند.
   */
  const handleCompanySwitch = async (tenant: TenantSummary, company: CompanySummary) => {
    if (activeCompany?.id === company.id) {
      setCompanyOpen(false);
      return;
    }
    setSwitching(company.id);
    try {
      const session = await refreshSession({ tenantId: tenant.id, companyId: company.id });
      useAuthStore.getState().setSession(session.accessToken, session.tenantId, session.companyId, session.permissions);
      useCompanyStore.getState().setCompanyContext(tenant, company);
      queryClient.clear();
      setCompanyOpen(false);
      navigate('/', { replace: true });
      toast.success(`زمینه کاری به «${company.name}» تغییر کرد`, 'همه داده‌های موقت شرکت قبلی پاک‌سازی شد.');
    } catch {
      toast.error('تغییر شرکت انجام نشد', 'کاربر عضو فعال این شرکت نیست یا نشست منقضی شده است.');
    } finally {
      setSwitching(null);
    }
  };

  const handleLogout = async () => {
    try {
      await logout();
    } finally {
      useCompanyStore.getState().clearCompanyContext();
      queryClient.clear();
      setUserOpen(false);
      navigate('/login', { replace: true });
    }
  };

  return (
    <header className="sticky top-0 z-10 flex h-16 items-center justify-between border-b border-border bg-surface px-4 sm:px-6">
      {/* Right side (RTL): Company switcher + fiscal badge */}
      <div className="flex min-w-0 items-center gap-2 sm:gap-3">
        <Popover open={companyOpen} onOpenChange={setCompanyOpen}>
          <PopoverTrigger asChild>
            <Button
              variant="outline"
              size="sm"
              className="gap-2 border-border/80 bg-muted/40 font-normal text-xs hover:bg-muted"
              aria-label="انتخاب شرکت فعال"
              disabled={switching !== null}
            >
              <Building className="size-4 text-primary" aria-hidden />
              <span className="min-w-0 truncate font-semibold text-foreground">
                {activeCompany?.name ?? 'انتخاب شرکت فعال'}
              </span>
              {activeTenant && (
                <span className="hidden text-[11px] text-muted-foreground sm:inline">({activeTenant.name})</span>
              )}
              <ChevronDown className="ms-1 size-3.5 text-muted-foreground" aria-hidden />
            </Button>
          </PopoverTrigger>
          <PopoverContent className="w-72 p-0" align="start">
            <p className="border-b px-3 py-2 text-xs font-semibold text-muted-foreground">شرکت‌های شما</p>
            <div className="thin-scroll max-h-72 overflow-y-auto p-1" role="listbox" aria-label="شرکت‌ها">
              {tenants.length === 0 ? (
                <p className="p-4 text-center text-xs text-muted-foreground">شرکتی یافت نشد</p>
              ) : (
                tenants.map((tenant) => (
                  <div key={tenant.id} className="px-1 pb-1">
                    <p className="px-2.5 pt-1.5 text-[11px] font-medium text-muted-foreground">{tenant.name}</p>
                    {tenant.companies.map((company) => {
                      const isActive = activeCompany?.id === company.id;
                      return (
                        <button
                          key={company.id}
                          type="button"
                          role="option"
                          aria-selected={isActive}
                          disabled={switching !== null}
                          onClick={() => handleCompanySwitch(tenant, company)}
                          className={cn(
                            'flex w-full cursor-pointer items-center justify-between gap-2 rounded-md px-2.5 py-2 text-start text-sm transition-colors hover:bg-muted disabled:opacity-60',
                            isActive && 'bg-primary-soft text-primary',
                          )}
                        >
                          <span className="truncate">{company.name}</span>
                          <span className="flex items-center gap-1.5">
                            <span className="text-[10px] text-muted-foreground">سال {digitsToPersian(company.fiscalYear)}</span>
                            {isActive && <Check className="size-4 text-primary" aria-hidden />}
                          </span>
                        </button>
                      );
                    })}
                  </div>
                ))
              )}
            </div>
          </PopoverContent>
        </Popover>

        {activeCompany && (
          <Badge variant="secondary" className="hidden items-center gap-1.5 md:inline-flex">
            <CalendarRange className="size-3.5" aria-hidden />
            سال مالی {digitsToPersian(activeCompany.fiscalYear)}
          </Badge>
        )}
      </div>

      {/* Left side (RTL): actions + user menu */}
      <div className="flex items-center gap-1 sm:gap-2">
        <Button variant="ghost" size="icon" onClick={toggleTheme} aria-label={theme === 'dark' ? 'تم روشن' : 'تم تاریک'}>
          {theme === 'dark' ? <Sun className="size-4 text-warning" aria-hidden /> : <Moon className="size-4 text-muted-foreground" aria-hidden />}
        </Button>

        <Tooltip content="مرکز اعلان‌ها پس از اتصال SignalR فعال می‌شود">
          <Button variant="ghost" size="icon" aria-label="اعلان‌ها" disabled>
            <Bell className="size-4 text-muted-foreground" aria-hidden />
          </Button>
        </Tooltip>

        <div className="mx-1 h-4 w-px bg-border" aria-hidden />

        <Popover open={userOpen} onOpenChange={setUserOpen}>
          <PopoverTrigger asChild>
            <button
              type="button"
              className="flex cursor-pointer items-center gap-2 rounded-md px-1.5 py-1 transition-colors hover:bg-muted"
              aria-label="منوی کاربری"
            >
              <Avatar name={user?.fullName || user?.email || 'کاربر'} size="sm" />
              <div className="hidden flex-col items-start lg:flex">
                <span className="text-xs font-semibold text-foreground">{user?.fullName || 'کاربر سیستم'}</span>
                <span className="max-w-40 truncate text-[10px] text-muted-foreground">{user?.email}</span>
              </div>
              <ChevronDown className="size-3.5 text-muted-foreground" aria-hidden />
            </button>
          </PopoverTrigger>
          <PopoverContent className="w-56 p-1.5" align="end">
            <div className="border-b px-2.5 pb-2 pt-1.5">
              <p className="truncate text-sm font-semibold text-foreground">{user?.fullName || 'کاربر سیستم'}</p>
              <p className="truncate text-xs text-muted-foreground">{user?.email}</p>
              {user?.roles && user.roles.length > 0 && (
                <div className="mt-1.5 flex flex-wrap gap-1">
                  {user.roles.map((role) => (
                    <Badge key={role} variant="secondary" className="text-[10px]">{role}</Badge>
                  ))}
                </div>
              )}
            </div>
            <button
              type="button"
              onClick={() => {
                setUserOpen(false);
                navigate('/profile');
              }}
              className="mt-1 flex w-full cursor-pointer items-center gap-2 rounded-md px-2.5 py-2 text-start text-sm text-foreground transition-colors hover:bg-muted"
            >
              <UserRound className="size-4 text-muted-foreground" aria-hidden />
              پروفایل کاربر
            </button>
            <button
              type="button"
              onClick={() => void handleLogout()}
              className="flex w-full cursor-pointer items-center gap-2 rounded-md px-2.5 py-2 text-start text-sm text-danger transition-colors hover:bg-danger/10"
            >
              <LogOut className="size-4" aria-hidden />
              خروج از حساب
            </button>
          </PopoverContent>
        </Popover>
      </div>
    </header>
  );
};
