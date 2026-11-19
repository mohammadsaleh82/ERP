/** مرکز حسابداری — دسترسی سریع به زیرصفحات واقعی این حوزه */
import * as React from 'react';
import { useNavigate } from 'react-router-dom';
import { ChevronLeft, FileText, ListTree, Settings2, CalendarRange } from 'lucide-react';
import { Card, CardContent } from '@/shared/components/ui/card';
import { useAuthStore } from '@/core/auth/auth.store';
import { PERMISSIONS } from '@/core/auth/permissions';

const links = [
  { label: 'فهرست اسناد حسابداری', href: '/accounting/documents', icon: FileText, permissions: [PERMISSIONS.documentsRead], description: 'صدور، تأیید و قطعی‌سازی اسناد' },
  { label: 'کدینگ حساب‌ها', href: '/accounting/accounts', icon: ListTree, permissions: [PERMISSIONS.accountsRead], description: 'درخت حساب‌ها، سطوح و تنظیمات تفصیلی' },
  { label: 'سال مالی و دوره‌ها', href: '/accounting/fiscal-years', icon: CalendarRange, permissions: [PERMISSIONS.fiscalYearsRead], description: 'ایجاد سال، تولید دوره‌ها و بستن دوره' },
  { label: 'تنظیمات حسابداری', href: '/accounting/settings', icon: Settings2, permissions: [PERMISSIONS.settingsRead], description: 'ارز پایه، گرد کردن، شماره‌گذاری و جریان تأیید' },
];

export const AccountingPage: React.FC = () => {
  const navigate = useNavigate();
  const hasAny = useAuthStore((state) => state.hasAnyPermission);
  const visible = links.filter((link) => hasAny(link.permissions));

  return (
    <div className="space-y-4" data-testid="accounting-hub">
      <div>
        <h1 className="text-lg font-black text-foreground">حسابداری مالی و دفاتر قانونی</h1>
        <p className="mt-0.5 text-xs text-muted-foreground">مدیریت درخت حساب‌ها، اسناد، دوره‌های مالی و تنظیمات مالی شرکت</p>
      </div>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        {visible.map((link) => (
          <Card key={link.href} className="cursor-pointer transition-colors hover:border-primary" onClick={() => navigate(link.href)}>
            <CardContent className="flex items-center justify-between gap-3 p-4">
              <span className="flex items-center gap-3">
                <span className="flex size-10 items-center justify-center rounded-lg bg-primary-soft text-primary">
                  <link.icon className="size-5" aria-hidden />
                </span>
                <span className="min-w-0">
                  <span className="block text-sm font-bold text-foreground">{link.label}</span>
                  <span className="block truncate text-[11px] text-muted-foreground">{link.description}</span>
                </span>
              </span>
              <ChevronLeft className="size-4 shrink-0 text-muted-foreground" aria-hidden />
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
};
