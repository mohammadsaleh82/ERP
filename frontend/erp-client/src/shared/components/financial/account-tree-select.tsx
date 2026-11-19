import * as React from 'react';
import { ChevronDown, ChevronLeft, FolderTree } from 'lucide-react';
import { Popover, PopoverContent, PopoverTrigger } from '@/shared/components/ui/popover';
import { cn } from '@/shared/utils/cn';

export interface AccountNode {
  id: string;
  /** کد حساب مثل 1101 */
  code: string;
  title: string;
  parentId: string | null;
  /** سطح حساب — فقط برگ‌ها قابل انتخاب (سطح تفصیلی) */
  selectable?: boolean;
  disabled?: boolean;
}

interface FlatTreeItem extends AccountNode {
  depth: number;
}

/** تبدیل لیست تخت به ردیف‌های سلسله‌مراتبی با ترتیب DFS */
function flattenTree(nodes: readonly AccountNode[]): FlatTreeItem[] {
  const byParent = new Map<string | null, AccountNode[]>();
  for (const node of nodes) {
    const list = byParent.get(node.parentId) ?? [];
    list.push(node);
    byParent.set(node.parentId, list);
  }
  const result: FlatTreeItem[] = [];
  const walk = (parent: string | null, depth: number) => {
    const children = byParent.get(parent) ?? [];
    for (const child of children) {
      result.push({ ...child, depth });
      walk(child.id, depth + 1);
    }
  };
  walk(null, 0);
  return result;
}

export interface AccountTreeSelectProps {
  nodes: readonly AccountNode[];
  value: string | null;
  onChange: (nodeId: string | null) => void;
  placeholder?: string;
  /** فقط برگ‌ها؟ پیش‌فرض: هر node با selectable=true */
  leavesOnly?: boolean;
  disabled?: boolean;
  className?: string;
  'aria-label'?: string;
}

/**
 * انتخاب حساب از درخت حساب‌ها (کد + عنوان) — انتخاب فقط در سطح تفصیلی.
 * داده تخت parentId محور؛ بدون وابستگی به API خاص.
 */
export const AccountTreeSelect: React.FC<AccountTreeSelectProps> = ({
  nodes,
  value,
  onChange,
  placeholder = 'انتخاب حساب…',
  leavesOnly = true,
  disabled,
  className,
  'aria-label': ariaLabel,
}) => {
  const [open, setOpen] = React.useState(false);
  const [collapsed, setCollapsed] = React.useState<ReadonlySet<string>>(new Set());
  const rows = React.useMemo(() => flattenTree(nodes), [nodes]);
  const selected = nodes.find((n) => n.id === value) ?? null;

  const visibleRows = rows.filter(
    (row) => {
      if (row.depth === 0) return true;
      // والد درخت تا این ردیف همه باید باز باشند
      const ancestors = rows.filter((r) => r.depth < row.depth).map((r) => r.id);
      return !ancestors.some((a) => collapsed.has(a));
    },
  );

  const toggle = (id: string) => {
    setCollapsed((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const hasChildren = (id: string): boolean => rows.some((r) => r.parentId === id);
  const canSelect = (row: FlatTreeItem): boolean =>
    !row.disabled && (leavesOnly ? !hasChildren(row.id) || row.selectable === true : row.selectable !== false);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <button
          type="button"
          disabled={disabled}
          aria-label={ariaLabel ?? placeholder}
          aria-expanded={open}
          className={cn(
            'flex h-9 w-full cursor-pointer items-center justify-between gap-2 rounded-md border border-input bg-surface px-3 text-sm shadow-xs transition-colors',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
            'disabled:cursor-not-allowed disabled:opacity-50',
            className,
          )}
        >
          <span className={cn('flex items-center gap-2 truncate', !selected && 'text-muted-foreground')}>
            <FolderTree className="size-4 shrink-0 text-muted-foreground" aria-hidden />
            {selected ? (
              <>
                <span className="font-code">{selected.code}</span>
                <span className="truncate">{selected.title}</span>
              </>
            ) : (
              placeholder
            )}
          </span>
          {selected && (
            <span
              role="button"
              tabIndex={0}
              aria-label="پاک کردن انتخاب"
              className="cursor-pointer text-muted-foreground hover:text-danger"
              onClick={(event) => {
                event.stopPropagation();
                onChange(null);
              }}
              onKeyDown={(event) => {
                if (event.key === 'Enter' || event.key === ' ') {
                  event.stopPropagation();
                  onChange(null);
                }
              }}
            >
              ×
            </span>
          )}
        </button>
      </PopoverTrigger>
      <PopoverContent className="w-80 p-0" align="start">
        <div className="thin-scroll max-h-72 overflow-y-auto p-1" role="tree" aria-label={ariaLabel ?? 'درخت حساب‌ها'}>
          {visibleRows.map((row) => {
            const isBranch = hasChildren(row.id);
            const isCollapsed = collapsed.has(row.id);
            const selectable = canSelect(row);
            return (
              <div
                key={row.id}
                role="treeitem"
                aria-expanded={isBranch ? !isCollapsed : undefined}
                aria-selected={row.id === value}
                style={{ paddingInlineStart: `${row.depth * 16 + 8}px` }}
                className={cn(
                  'flex w-full items-center gap-1.5 rounded-md py-1.5 text-sm transition-colors',
                  row.id === value ? 'bg-primary-soft text-primary' : 'hover:bg-muted',
                )}
              >
                {isBranch ? (
                  <button
                    type="button"
                    aria-label={isCollapsed ? 'باز کردن' : 'بستن'}
                    className="cursor-pointer text-muted-foreground"
                    onClick={() => toggle(row.id)}
                  >
                    {isCollapsed ? <ChevronLeft className="size-4" aria-hidden /> : <ChevronDown className="size-4" aria-hidden />}
                  </button>
                ) : (
                  <span className="size-4" aria-hidden />
                )}
                <button
                  type="button"
                  disabled={!selectable}
                  onClick={() => {
                    if (!selectable) return;
                    onChange(row.id);
                    setOpen(false);
                  }}
                  className={cn(
                    'flex flex-1 cursor-pointer items-center justify-between gap-2 text-start',
                    !selectable && 'cursor-default',
                  )}
                >
                  <span className="flex items-center gap-2">
                    <span className="font-code text-xs text-muted-foreground">{row.code}</span>
                    <span className="truncate">{row.title}</span>
                  </span>
                  {isBranch && !leavesOnly && row.selectable !== false && (
                    <span className="text-[10px] text-muted-foreground">سطح کلان</span>
                  )}
                </button>
              </div>
            );
          })}
        </div>
      </PopoverContent>
    </Popover>
  );
};
