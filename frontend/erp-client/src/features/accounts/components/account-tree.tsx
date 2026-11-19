/**
 * درخت حساب‌ها — عمق متغیر از API (نه چهار سطح ثابت):
 * هر نود Level واقعی خود را از backend دارد و فرزندان بازگشتی رندر می‌شوند؛
 * سطح تفصیلی (Detail) خودش چندسطحی است و درخت بدون فرض تعداد سطح، هر عمقی را نشان می‌دهد.
 */
import * as React from 'react';
import { ChevronDown, ChevronLeft, Landmark, Wallet } from 'lucide-react';
import { Badge } from '@/shared/components/ui/badge';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { EmptyState } from '@/shared/components/feedback/empty-state';
import { ErrorState } from '@/shared/components/feedback/error-state';
import { cn } from '@/shared/utils/cn';
import { levelLabel } from '@/features/accounts/account-labels';
import type { AccountTreeNodeDto } from '@/core/accounting/accounts.types';

export interface AccountTreeProps {
  nodes: AccountTreeNodeDto[];
  isLoading?: boolean;
  isError?: boolean;
  errorDetail?: string;
  onRetry?: () => void;
  selectedId?: string | null;
  onSelect?: (node: AccountTreeNodeDto) => void;
  /** فیلتر جست‌وجوی محلی — نام یا کد */
  filterTerm?: string;
  /** فقط شاخه‌های قابل‌ثبت با خودِ قابل‌ثبت هایلایت */
  postableOnly?: boolean;
  emptyMessage?: string;
  /** باز کردن اجباری همه شاخه‌ها (برای جست‌وجو) */
  expandAll?: boolean;
}

/** نگه‌داشتن وضعیت باز/بسته در والد — باز شدن پیش‌فرض: ریشه‌ها */
export const AccountTree: React.FC<AccountTreeProps> = ({
  nodes,
  isLoading,
  isError,
  errorDetail,
  onRetry,
  selectedId,
  onSelect,
  filterTerm,
  postableOnly,
  emptyMessage = 'حسابی ثبت نشده است',
  expandAll,
}) => {
  const [expanded, setExpanded] = React.useState<Set<string>>(new Set());

  React.useEffect(() => {
    if (expandAll) {
      // با expandAll (جست‌وجو یا درخواست صفحه) همه شاخه‌ها باز می‌شوند
      const all = new Set<string>();
      const walk = (list: AccountTreeNodeDto[]) => {
        for (const node of list) {
          if (node.children.length > 0) all.add(node.id);
          walk(node.children);
        }
      };
      walk(nodes);
      setExpanded(all);
    }
  }, [expandAll, filterTerm, nodes]);

  const toggle = React.useCallback((id: string) => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);

  // فیلتر محلی: نودهایی که خودشان یا فرزندانشان با عبارت می‌خوانند
  const matchesTerm = React.useCallback(
    (node: AccountTreeNodeDto): boolean => {
      if (!filterTerm?.trim()) return true;
      const term = filterTerm.trim();
      if (node.name.includes(term) || node.fullCode.includes(term) || node.code.includes(term)) return true;
      return node.children.some(matchesTerm);
    },
    [filterTerm],
  );

  if (isLoading) {
    return (
      <div className="space-y-2 p-4" aria-busy="true" role="status">
        {Array.from({ length: 6 }).map((_, index) => (
          <Skeleton key={index} className="h-8" style={{ width: `${95 - index * 8}%` }} />
        ))}
      </div>
    );
  }

  if (isError) {
    return <ErrorState title="بارگذاری درخت حساب‌ها ناموفق بود" description={errorDetail} onRetry={onRetry} />;
  }

  const visible = nodes.filter(matchesTerm);
  if (visible.length === 0) {
    return <EmptyState title={emptyMessage} description={filterTerm ? 'عبارت دیگری را امتحان کنید' : undefined} />;
  }

  return (
    <ul className="space-y-0.5 p-2" data-testid="account-tree">
      {visible.map((node) => (
        <TreeNode
          key={node.id}
          node={node}
          depth={0}
          expanded={expanded}
          toggle={toggle}
          selectedId={selectedId}
          onSelect={onSelect}
          postableOnly={postableOnly}
        />
      ))}
    </ul>
  );
};

interface TreeNodeProps {
  node: AccountTreeNodeDto;
  depth: number;
  expanded: Set<string>;
  toggle: (id: string) => void;
  selectedId?: string | null;
  onSelect?: (node: AccountTreeNodeDto) => void;
  postableOnly?: boolean;
}

/** خود حساب یا هر نادِ آن قابل‌ثبت است؟ (شاخه تجمیعی مسیرِ حساب قابل‌ثبت حفظ می‌شود) */
const hasPostingDescendant = (node: AccountTreeNodeDto): boolean =>
  node.allowPosting || node.children.some(hasPostingDescendant);

const TreeNode: React.FC<TreeNodeProps> = ({ node, depth, expanded, toggle, selectedId, onSelect, postableOnly }) => {
  const hasChildren = node.children.length > 0;
  const isOpen = expanded.has(node.id);
  const isSelected = selectedId === node.id;

  if (postableOnly && !hasPostingDescendant(node)) return null;

  return (
    <li>
      <div
        className={cn(
          'group flex cursor-pointer items-center gap-1 rounded-md px-1.5 py-1.5 transition-colors hover:bg-muted',
          isSelected && 'bg-primary-soft ring-1 ring-primary/40',
        )}
        style={{ paddingInlineStart: `${depth * 16 + 6}px` }}
        onClick={() => onSelect?.(node)}
        role="treeitem"
        aria-selected={isSelected}
        aria-expanded={hasChildren ? isOpen : undefined}
        tabIndex={0}
        onKeyDown={(event) => {
          if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            onSelect?.(node);
          }
        }}
        data-testid={`tree-node-${node.fullCode}`}
      >
        {hasChildren ? (
          <button
            type="button"
            className="flex size-5 shrink-0 cursor-pointer items-center justify-center rounded hover:bg-background"
            aria-label={isOpen ? `بستن شاخه ${node.name}` : `باز کردن شاخه ${node.name}`}
            onClick={(event) => {
              event.stopPropagation();
              toggle(node.id);
            }}
          >
            {isOpen ? <ChevronDown className="size-3.5 text-muted-foreground" /> : <ChevronLeft className="size-3.5 text-muted-foreground" />}
          </button>
        ) : (
          <span className="flex size-5 shrink-0 items-center justify-center">
            {node.allowPosting ? <Wallet className="size-3 text-primary/70" aria-hidden /> : <span className="size-1.5 rounded-full bg-border" aria-hidden />}
          </span>
        )}
        <span dir="ltr" className="font-code shrink-0 text-[11px] text-muted-foreground">
          {node.fullCode}
        </span>
        <span className={cn('min-w-0 truncate text-xs font-medium', !node.isActive && 'text-muted-foreground line-through')}>
          {node.name}
        </span>
        <span className="ms-auto flex shrink-0 items-center gap-1 opacity-80 transition-opacity group-hover:opacity-100">
          {!node.isActive && <Badge variant="secondary" className="px-1 py-0 text-[9px]">غیرفعال</Badge>}
          {node.allowPosting && <Badge variant="success" className="px-1 py-0 text-[9px]">قابل‌ثبت</Badge>}
          <Badge variant="outline" className="hidden px-1 py-0 text-[9px] sm:inline">{levelLabel(node.level)}</Badge>
        </span>
      </div>
      {hasChildren && isOpen && (
        <ul role="group">
          {node.children.map((child) => (
            <TreeNode
              key={child.id}
              node={child}
              depth={depth + 1}
              expanded={expanded}
              toggle={toggle}
              selectedId={selectedId}
              onSelect={onSelect}
              postableOnly={postableOnly}
            />
          ))}
        </ul>
      )}
    </li>
  );
};

export { Landmark };
