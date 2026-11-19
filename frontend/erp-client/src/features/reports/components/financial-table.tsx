/**
 * جدول مالی مشترک — خوانایی بالا برای اعداد و مانده‌ها:
 * فرمت هزارگان تبلار، تفکیک بصری بدهکار/بستانکار، بج جهت مانده + هشدار مانده غیرعادی،
 * سلول شماره سند با کپی و لینک به سند، Tooltip برای شرح طولانی، ریسپانسیو
 * (اسکرول افقی داخل container) + Loading/Empty و footer جمع‌ها.
 */
import * as React from 'react';
import { ArrowLeftRight, Copy, Link2 } from 'lucide-react';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { EmptyState } from '@/shared/components/feedback/empty-state';
import { Button } from '@/shared/components/ui/button';
import { toast } from '@/core/notifications/toast';
import { formatAmount, parseAmountInput } from '@/core/accounting/documents.service';
import { cn } from '@/shared/utils/cn';

/** سلول مبلغ — بدهکار آبی، بستانکار کهربایی؛ صفر → — */
export const MoneyCell: React.FC<{ value: string | number | null; side?: 'debit' | 'credit'; testId?: string }> = ({ value, side, testId }) => {
  const isZero = !value || parseAmountInput(String(value)) === '' || Number(value) === 0;
  return (
    <span dir="ltr" className={cn('inline-block tabular-nums', side === 'debit' && 'text-sky-700 dark:text-sky-400', side === 'credit' && 'text-amber-700 dark:text-amber-400')} data-testid={testId}>
      {isZero ? '—' : formatAmount(value)}
    </span>
  );
};

export const directionLabel = (direction: string): string => (direction === 'Debtor' ? 'بدهکار' : direction === 'Creditor' ? 'بستانکار' : 'بدون مانده');

/** سلول مانده — مقدار + جهت با تفکیک بصری؛ مانده غیرعادی با هشدار */
export const BalanceCell: React.FC<{ amount: string; direction?: string; abnormal?: boolean; testId?: string }> = ({ amount, direction, abnormal, testId }) => (
  <span className="inline-flex items-center gap-1 whitespace-nowrap" data-testid={testId}>
    <MoneyCell value={amount} side={direction === 'Debtor' ? 'debit' : direction === 'Creditor' ? 'credit' : undefined} />
    {direction && direction !== 'None' && (
      <span className={cn('rounded px-1 py-0.5 text-[9px] font-bold', direction === 'Debtor' ? 'bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-300' : 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300')}>
        {directionLabel(direction)}
      </span>
    )}
    {abnormal && (
      <span className="inline-flex items-center text-danger" title="مانده خلاف ماهیت عادی حساب (IsAbnormalBalance)">
        <ArrowLeftRight className="size-3" aria-hidden />
      </span>
    )}
  </span>
);

/** شماره سند با کپی + لینک مشاهده سند */
export const DocNumberCell: React.FC<{ documentNumber: number; documentId?: string | null; testId?: string }> = ({ documentNumber, documentId, testId }) => {
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(String(documentNumber));
      toast.success('شماره سند کپی شد', String(documentNumber));
    } catch {
      toast.error('کپی انجام نشد', 'دسترسی به کلیپ‌بورد ممکن نیست');
    }
  };
  return (
    <span className="inline-flex items-center gap-1 whitespace-nowrap" data-testid={testId}>
      {documentId ? (
        <a href={`/accounting/documents/${documentId}`} className="inline-flex items-center gap-1 font-semibold text-primary hover:underline" title="مشاهده سند مرجع" aria-label={`مشاهده سند ${documentNumber}`}>
          {documentNumber}
          <Link2 className="size-3" aria-hidden />
        </a>
      ) : (
        <span className="font-semibold">{documentNumber}</span>
      )}
      <Button variant="ghost" size="icon" className="size-5" onClick={() => void copy()} aria-label={`کپی شماره سند ${documentNumber}`} data-testid={testId ? `${testId}-copy` : undefined}>
        <Copy className="size-3" aria-hidden />
      </Button>
    </span>
  );
};

/** شرح با Tooltip در حالت طولانی */
export const DescriptionCell: React.FC<{ text: string; maxChars?: number }> = ({ text, maxChars = 42 }) => {
  if (!text) return <span className="text-muted-foreground">—</span>;
  if (text.length <= maxChars) return <span>{text}</span>;
  return (
    <span title={text} className="cursor-help">
      {text.slice(0, maxChars)}…
    </span>
  );
};

export interface FinancialColumn<T> {
  key: string;
  header: string;
  align?: 'start' | 'end' | 'center';
  render: (row: T, index: number) => React.ReactNode;
  className?: string;
}

export interface FinancialTableProps<T> {
  columns: FinancialColumn<T>[];
  rows: T[] | undefined;
  rowKey: (row: T, index: number) => string;
  loading?: boolean;
  emptyTitle?: string;
  emptyDescription?: string;
  footer?: React.ReactNode;
  ariaLabel?: string;
}

export function FinancialTable<T>({ columns, rows, rowKey, loading, emptyTitle = 'داده‌ای برای نمایش نیست', emptyDescription = 'فیلترها را تغییر دهید یا بازه زمانی دیگری انتخاب کنید.', footer, ariaLabel }: FinancialTableProps<T>) {
  if (loading) {
    return (
      <div className="space-y-2 p-4" aria-busy="true" role="status" data-testid="financial-table-loading">
        {Array.from({ length: 6 }).map((_, index) => <Skeleton key={index} className="h-8 w-full" />)}
      </div>
    );
  }
  if (!rows || rows.length === 0) {
    return <EmptyState title={emptyTitle} description={emptyDescription} />;
  }
  return (
    <div className="overflow-x-auto" data-testid="financial-table">
      <table className="w-full text-xs" aria-label={ariaLabel}>
        <thead className="bg-muted/40 text-muted-foreground">
          <tr>
            {columns.map((column) => (
              <th key={column.key} scope="col" className={cn('whitespace-nowrap px-2.5 py-2 font-semibold', column.align === 'end' ? 'text-end' : column.align === 'center' ? 'text-center' : 'text-start', column.className)}>
                {column.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, index) => (
            <tr key={rowKey(row, index)} className="border-t border-border/40 hover:bg-muted/30">
              {columns.map((column) => (
                <td key={column.key} className={cn('px-2.5 py-2', column.align === 'end' ? 'text-end' : column.align === 'center' ? 'text-center' : 'text-start', column.className)}>
                  {column.render(row, index)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
        {footer && <tfoot className="border-t-2 border-border bg-muted/30 font-bold">{footer}</tfoot>}
      </table>
    </div>
  );
}

/** نوار جمع پایین جدول */
export const TotalsFooter: React.FC<{ cells: React.ReactNode[]; colSpanBefore?: number }> = ({ cells, colSpanBefore = 0 }) => (
  <tr data-testid="totals-footer">
    {colSpanBefore > 0 && <td colSpan={colSpanBefore} className="px-2.5 py-2">جمع کل</td>}
    {cells.map((cell, index) => (
      <td key={index} className="px-2.5 py-2">{cell}</td>
    ))}
  </tr>
);
