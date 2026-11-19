import React from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { menuItems } from '@/app/config/menu';
import { useCompanyStore } from '@/core/company/company.store';
import { useAuthStore } from '@/core/auth/auth.store';
import { cn } from '@/shared/utils/cn';

/**
 * Sidebar با فیلتر مجوز واقعی، active state پایدار (NavLink) و جمع‌شدن پایدار.
 * آیتم‌های بدون مجوز حذف می‌شوند — نه disabled.
 */
export const Sidebar: React.FC = () => {
  const { sidebarCollapsed, toggleSidebar, activeCompany, activeTenant } = useCompanyStore();
  const hasAny = useAuthStore((state) => state.hasAnyPermission);
  const location = useLocation();

  const visibleItems = menuItems.filter((item) => !item.permissions || hasAny(item.permissions));

  return (
    <aside
      className={cn(
        'relative flex flex-col border-e border-border bg-surface transition-[width] duration-200 z-20 shrink-0 h-screen sticky top-0',
        sidebarCollapsed ? 'w-16' : 'w-64',
      )}
    >
      {/* Brand */}
      <div className="flex h-16 items-center justify-between border-b border-border px-3">
        {!sidebarCollapsed ? (
          <div className="flex min-w-0 items-center gap-2">
            <div className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-primary font-black text-lg text-primary-foreground">
              E
            </div>
            <div className="flex min-w-0 flex-col">
              <span className="truncate text-sm font-bold leading-tight text-foreground">سامانه سازمانی ERP</span>
              <span className="truncate text-[10px] text-muted-foreground">
                {activeCompany ? `${activeCompany.name} · ${activeTenant?.name ?? ''}` : 'بدون شرکت فعال'}
              </span>
            </div>
          </div>
        ) : (
          <div className="mx-auto flex size-8 items-center justify-center rounded-lg bg-primary font-black text-lg text-primary-foreground">
            E
          </div>
        )}
        <button
          type="button"
          onClick={toggleSidebar}
          aria-label={sidebarCollapsed ? 'گسترش منو' : 'جمع کردن منو'}
          className="cursor-pointer rounded-md p-1 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
        >
          {sidebarCollapsed ? <ChevronLeft className="size-4" aria-hidden /> : <ChevronRight className="size-4" aria-hidden />}
        </button>
      </div>

      {/* Nav */}
      <nav aria-label="منوی اصلی" className="thin-scroll flex-1 space-y-1 overflow-y-auto px-2 py-4">
        {visibleItems.map((item) => {
          const Icon = item.icon;
          return (
            <NavLink
              key={item.href}
              to={item.href}
              end={item.href === '/'}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-3 select-none rounded-md px-3 py-2.5 text-sm font-medium transition-colors',
                  isActive
                    ? 'bg-primary font-semibold text-primary-foreground shadow-xs'
                    : 'text-muted-foreground hover:bg-muted hover:text-foreground',
                  sidebarCollapsed && 'justify-center px-0',
                )
              }
              title={sidebarCollapsed ? item.label : undefined}
              aria-current={location.pathname === item.href ? 'page' : undefined}
            >
              <Icon className="size-5 shrink-0" aria-hidden />
              {!sidebarCollapsed && <span>{item.label}</span>}
            </NavLink>
          );
        })}
      </nav>

      <div className="border-t border-border p-3 text-center">
        {!sidebarCollapsed ? (
          <p className="text-xs text-muted-foreground">طراحی‌شده برای مقیاس سازمانی</p>
        ) : (
          <span className="text-[10px] text-muted-foreground">ERP</span>
        )}
      </div>
    </aside>
  );
};
