import * as React from 'react';
import { ShieldAlert } from 'lucide-react';
import { WidgetShell } from '@/shared/components/widget/widget-shell';
import { useCompanyStore } from '@/core/company/company.store';
import { digitsToPersian } from '@/shared/utils/number';

/**
 * هشدارهای مالی — Backend endpoint تجمیعی هشدار ندارد؛
 * تنها سیگنال واقعی موجود بدون ساخت عدد: سال/دوره بسته نزدیک؟
 * در قرارداد فعلی «فاصله تا پایان دوره» قابل محاسبه از FiscalYearDto باز است.
 */
export const FinancialAlerts: React.FC = () => {
  const activeCompany = useCompanyStore((state) => state.activeCompany);

  // بدون API هشدار: هیچ عدد ساختگی نمایش داده نمی‌شود → Empty با نیاز Backend
  const alerts: Array<{ text: string }> = [];
  void activeCompany;

  return (
    <WidgetShell
      title="هشدارهای مالی"
      isEmpty={alerts.length === 0}
      emptyMessage="هیچ هشداری برای نمایش وجود ندارد"
      requiresBackend
    >
      <ul className="space-y-2">
        {alerts.map((alert) => (
          <li key={alert.text} className="flex items-center gap-2 rounded-md border border-warning/30 bg-warning/5 px-3 py-2 text-xs text-foreground">
            <ShieldAlert className="size-4 text-warning" aria-hidden />
            {alert.text}
          </li>
        ))}
      </ul>
      {activeCompany && (
        <p className="mt-2 text-[10px] text-muted-foreground">
          سال مالی جاری: {digitsToPersian(activeCompany.fiscalYear)}
        </p>
      )}
    </WidgetShell>
  );
};
