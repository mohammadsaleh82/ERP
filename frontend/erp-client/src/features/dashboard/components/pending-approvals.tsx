import * as React from 'react';
import { useNavigate } from 'react-router-dom';
import { ChevronLeft } from 'lucide-react';
import { Badge } from '@/shared/components/ui/badge';
import { WidgetShell } from '@/shared/components/widget/widget-shell';
import { useDashboardData } from '@/features/dashboard/dashboard.queries';
import { digitsToPersian, formatNumberPersian } from '@/shared/utils/number';
import { formatToPersianDate } from '@/shared/utils/date';
import { statusToLabel } from '@/features/dashboard/components/status-labels';

/** اسناد در انتظار تأیید — داده واقعی SearchDocuments با Status=PendingApproval */
export const PendingApprovals: React.FC = () => {
  const navigate = useNavigate();
  const { pending, canDocuments } = useDashboardData({});
  const items = pending.data?.items ?? [];

  return (
    <WidgetShell
      title="اسناد در انتظار تأیید"
      isLoading={pending.isLoading}
      isError={pending.isError}
      isUnauthorized={!canDocuments}
      isEmpty={items.length === 0}
      emptyMessage="سندی در جریان تأیید وجود ندارد"
      onRetry={() => void pending.refetch()}
      action={
        items.length > 0 ? (
          <Badge variant="warning">{digitsToPersian(pending.data?.totalCount ?? 0)}</Badge>
        ) : undefined
      }
    >
      <ul className="space-y-2">
        {items.map((doc) => (
          <li key={doc.id}>
            <button
              type="button"
              onClick={() => navigate('/accounting/documents')}
              className="flex w-full cursor-pointer items-center justify-between gap-2 rounded-md border border-border/60 px-3 py-2 text-start transition-colors hover:border-primary hover:bg-primary-soft/40"
            >
              <span className="min-w-0">
                <span className="block text-xs font-semibold text-foreground">
                  سند شماره {digitsToPersian(doc.documentNumber)}
                </span>
                <span className="block truncate text-[11px] text-muted-foreground">{doc.description || '—'}</span>
              </span>
              <span className="flex shrink-0 items-center gap-2">
                <span className="hidden text-[11px] tabular-nums text-muted-foreground sm:inline">
                  {formatNumberPersian(doc.totalDebit)}
                </span>
                <span className="text-[10px]">{formatToPersianDate(doc.documentDate)}</span>
                <Badge variant="secondary">{statusToLabel(doc.status)}</Badge>
                <ChevronLeft className="size-3.5 text-muted-foreground" aria-hidden />
              </span>
            </button>
          </li>
        ))}
      </ul>
    </WidgetShell>
  );
};
