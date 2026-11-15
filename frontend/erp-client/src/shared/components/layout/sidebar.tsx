import React from 'react';
import { NavLink } from 'react-router-dom';
import {
  LayoutDashboard,
  ShieldCheck,
  Building2,
  Receipt,
  Landmark,
  Boxes,
  ShoppingCart,
  TrendingUp,
  Users2,
  BadgeDollarSign,
  BarChart3,
  Settings,
  ChevronRight,
  ChevronLeft,
} from 'lucide-react';
import { useCompanyStore } from '@/core/company/company.store';
import { cn } from '@/shared/utils/cn';

interface NavItem {
  label: string;
  href: string;
  icon: React.ElementType;
}

const navItems: NavItem[] = [
  { label: 'داشبورد مدیریتی', href: '/', icon: LayoutDashboard },
  { label: 'حسابداری مالی', href: '/accounting', icon: Receipt },
  { label: 'خزانه‌داری و چک', href: '/treasury', icon: Landmark },
  { label: 'انبار و موجودی', href: '/inventory', icon: Boxes },
  { label: 'فروش و بازرگانی', href: '/sales', icon: TrendingUp },
  { label: 'خرید و تدارکات', href: '/purchasing', icon: ShoppingCart },
  { label: 'طرف‌حساب‌ها', href: '/parties', icon: Users2 },
  { label: 'حقوق و دستمزد', href: '/payroll', icon: BadgeDollarSign },
  { label: 'گزارش‌های جامع', href: '/reporting', icon: BarChart3 },
  { label: 'ساختار سازمان', href: '/organization', icon: Building2 },
  { label: 'کاربران و امنیت', href: '/identity', icon: ShieldCheck },
  { label: 'تنظیمات و پلن‌ها', href: '/settings', icon: Settings },
];

export const Sidebar: React.FC = () => {
  const { sidebarCollapsed, toggleSidebar } = useCompanyStore();

  return (
    <aside
      className={cn(
        'relative flex flex-col border-e border-border bg-surface transition-all duration-300 z-20 shrink-0 h-screen sticky top-0',
        sidebarCollapsed ? 'w-16' : 'w-64'
      )}
    >
      {/* Brand Header */}
      <div className="flex items-center justify-between h-16 px-4 border-b border-border">
        {!sidebarCollapsed && (
          <div className="flex items-center gap-2">
            <div className="w-8 h-8 rounded-lg bg-primary flex items-center justify-center text-primary-foreground font-black text-lg">
              E
            </div>
            <div className="flex flex-col">
              <span className="font-bold text-sm leading-tight text-foreground">سامانه سازمانی ERP</span>
              <span className="text-[10px] text-muted-foreground">نسخه ۱.۰.۰ ابری</span>
            </div>
          </div>
        )}
        {sidebarCollapsed && (
          <div className="mx-auto w-8 h-8 rounded-lg bg-primary flex items-center justify-center text-primary-foreground font-black text-lg">
            E
          </div>
        )}
        <button
          onClick={toggleSidebar}
          className="p-1 rounded-md text-muted-foreground hover:bg-muted hover:text-foreground transition-colors cursor-pointer"
          title={sidebarCollapsed ? 'گسترش منو' : 'جمع کردن منو'}
        >
          {sidebarCollapsed ? <ChevronLeft className="w-4 h-4" /> : <ChevronRight className="w-4 h-4" />}
        </button>
      </div>

      {/* Navigation List */}
      <nav className="flex-1 overflow-y-auto px-2 py-4 space-y-1">
        {navItems.map((item) => {
          const Icon = item.icon;
          return (
            <NavLink
              key={item.href}
              to={item.href}
              end={item.href === '/'}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-3 px-3 py-2.5 rounded-md text-sm font-medium transition-colors select-none',
                  isActive
                    ? 'bg-primary text-primary-foreground font-semibold shadow-xs'
                    : 'text-muted-foreground hover:bg-muted hover:text-foreground',
                  sidebarCollapsed && 'justify-center px-0'
                )
              }
              title={sidebarCollapsed ? item.label : undefined}
            >
              <Icon className="w-5 h-5 shrink-0" />
              {!sidebarCollapsed && <span>{item.label}</span>}
            </NavLink>
          );
        })}
      </nav>

      {/* Sidebar Footer */}
      <div className="p-3 border-t border-border text-center">
        {!sidebarCollapsed ? (
          <p className="text-xs text-muted-foreground">طراحی‌شده برای مقیاس سازمانی</p>
        ) : (
          <span className="text-[10px] text-muted-foreground">ERP</span>
        )}
      </div>
    </aside>
  );
};
