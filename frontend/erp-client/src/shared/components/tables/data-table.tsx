import React from 'react';
import { cn } from '@/shared/utils/cn';
import { LoadingState } from '@/shared/components/feedback/loading-state';
import { EmptyState } from '@/shared/components/feedback/empty-state';

export interface Column<T> {
  key: string;
  header: string;
  render?: (item: T) => React.ReactNode;
  align?: 'start' | 'center' | 'end';
  width?: string;
  isNumeric?: boolean;
}

interface DataTableProps<T> {
  columns: Column<T>[];
  data: T[];
  isLoading?: boolean;
  emptyTitle?: string;
  emptyDescription?: string;
  onRowClick?: (item: T) => void;
  className?: string;
}

export function DataTable<T extends { id?: string | number }>({
  columns,
  data,
  isLoading = false,
  emptyTitle,
  emptyDescription,
  onRowClick,
  className,
}: DataTableProps<T>) {
  if (isLoading) {
    return <LoadingState className="py-16" />;
  }

  if (data.length === 0) {
    return <EmptyState title={emptyTitle} description={emptyDescription} className="my-6" />;
  }

  return (
    <div className={cn('w-full overflow-x-auto rounded-lg border border-border bg-surface shadow-sm', className)}>
      <table className="w-full text-sm text-start border-collapse">
        <thead className="bg-muted/70 text-muted-foreground font-semibold border-b border-border">
          <tr>
            {columns.map((col) => (
              <th
                key={col.key}
                style={{ width: col.width }}
                className={cn(
                  'px-4 py-3 text-start font-medium select-none',
                  col.align === 'end' && 'text-end',
                  col.align === 'center' && 'text-center'
                )}
              >
                {col.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {data.map((item, index) => (
            <tr
              key={item.id ?? index}
              onClick={() => onRowClick?.(item)}
              className={cn(
                'transition-colors hover:bg-muted/50',
                onRowClick && 'cursor-pointer',
                index % 2 === 1 && 'bg-muted/20'
              )}
            >
              {columns.map((col) => (
                <td
                  key={col.key}
                  className={cn(
                    'px-4 py-3 text-foreground',
                    col.isNumeric && 'font-tabular',
                    col.align === 'end' && 'text-end',
                    col.align === 'center' && 'text-center'
                  )}
                >
                  {col.render ? col.render(item) : (item as Record<string, unknown>)[col.key] as React.ReactNode}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
