import * as React from 'react';
import { useNavigate } from 'react-router-dom';
import { FilePlus2, FileText, Landmark, Scale, Wallet } from 'lucide-react';
import { WidgetShell } from '@/shared/components/widget/widget-shell';
import { useAuthStore } from '@/core/auth/auth.store';
import { PERMISSIONS } from '@/core/auth/permissions';

interface QuickAction {
  label: string;
  href: string;
  icon: React.ElementType;
  permissions?: readonly string[];
}

const actions: QuickAction[] = [
  { label: 'سند جدید', href: '/accounting/documents', icon: FilePlus2, permissions: [PERMISSIONS.documentsCreate] },
  { label: 'فهرست اسناد', href: '/accounting/documents', icon: FileText, permissions: [PERMISSIONS.documentsRead] },
  { label: 'مانده‌های حساب', href: '/reporting', icon: Wallet, permissions: [PERMISSIONS.ledgerRead] },
  { label: 'دفتر آزمایشی', href: '/reporting', icon: Scale, permissions: [PERMISSIONS.reportsRead] },
  { label: 'کدینگ حساب‌ها', href: '/accounting/accounts', icon: Landmark, permissions: [PERMISSIONS.accountsRead] },
];

/** میانبر عملیات پرتکرار — فقط با مجوز واقعی نمایش داده می‌شود */
export const QuickActions: React.FC = () => {
  const navigate = useNavigate();
  const hasAny = useAuthStore((state) => state.hasAnyPermission);
  const visible = actions.filter((action) => !action.permissions || hasAny(action.permissions));

  return (
    <WidgetShell title="عملیات پرتکرار" isEmpty={visible.length === 0} emptyMessage="میانبری با مجوزهای شما در دسترس نیست">
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-5">
        {visible.map((action) => (
          <button
            key={action.label}
            type="button"
            onClick={() => navigate(action.href)}
            className="flex cursor-pointer flex-col items-center gap-2 rounded-lg border border-border/60 bg-surface px-3 py-4 text-xs font-semibold text-foreground transition-colors hover:border-primary hover:bg-primary-soft/50"
          >
            <action.icon className="size-5 text-primary" aria-hidden />
            {action.label}
          </button>
        ))}
      </div>
    </WidgetShell>
  );
};
