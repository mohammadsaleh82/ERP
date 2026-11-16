import React from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Building,
  Moon,
  Sun,
  Bell,
  LogOut,
  User,
  ChevronDown,
} from 'lucide-react';
import { useCompanyStore } from '@/core/company/company.store';
import { useAuthStore } from '@/core/auth/auth.store';
import { Button } from '@/shared/components/ui/button';

export const Header: React.FC = () => {
  const navigate = useNavigate();
  const { activeTenant, activeCompany, theme, toggleTheme } = useCompanyStore();
  const { user, clearAuth } = useAuthStore();

  const handleLogout = () => {
    clearAuth();
    navigate('/login');
  };

  return (
    <header className="h-16 border-b border-border bg-surface px-6 flex items-center justify-between sticky top-0 z-10">
      {/* Left (in RTL: Right side) - Active Company Selector Pill */}
      <div className="flex items-center gap-3">
        <Button
          variant="outline"
          size="sm"
          onClick={() => navigate('/select-company')}
          className="flex items-center gap-2 border-border/80 bg-muted/40 hover:bg-muted font-normal text-xs"
        >
          <Building className="w-4 h-4 text-primary" />
          <div className="flex items-center gap-1.5">
            <span className="font-semibold text-foreground">
              {activeCompany?.name || 'انتخاب شرکت فعال'}
            </span>
            {activeTenant && (
              <span className="text-muted-foreground text-[11px]">
                ({activeTenant.name})
              </span>
            )}
          </div>
          <ChevronDown className="w-3.5 h-3.5 text-muted-foreground ms-1" />
        </Button>
      </div>

      {/* Right (in RTL: Left side) - Actions & User Profile */}
      <div className="flex items-center gap-2">
        {/* Dark/Light Mode Toggle */}
        <Button
          variant="ghost"
          size="icon"
          onClick={toggleTheme}
          title={theme === 'dark' ? 'تم روشن' : 'تم تاریک'}
        >
          {theme === 'dark' ? (
            <Sun className="w-4 h-4 text-warning" />
          ) : (
            <Moon className="w-4 h-4 text-muted-foreground" />
          )}
        </Button>

        {/* Notifications */}
        <Button variant="ghost" size="icon" title="اعلان‌ها">
          <Bell className="w-4 h-4 text-muted-foreground" />
        </Button>

        <div className="h-4 w-px bg-border mx-1" />

        {/* User Info & Logout */}
        <div className="flex items-center gap-2">
          <div className="w-8 h-8 rounded-full bg-primary/10 text-primary flex items-center justify-center font-bold text-xs">
            <User className="w-4 h-4" />
          </div>
          <div className="hidden md:flex flex-col text-start">
            <span className="text-xs font-semibold text-foreground">
              {user?.fullName || 'کاربر سیستم'}
            </span>
            <span className="text-[10px] text-muted-foreground">
              {user?.email || 'admin@erp.local'}
            </span>
          </div>

          <Button
            variant="ghost"
            size="icon"
            onClick={handleLogout}
            title="خروج از حساب کاربری"
            className="text-danger hover:text-danger hover:bg-danger/10 ms-1"
          >
            <LogOut className="w-4 h-4" />
          </Button>
        </div>
      </div>
    </header>
  );
};
