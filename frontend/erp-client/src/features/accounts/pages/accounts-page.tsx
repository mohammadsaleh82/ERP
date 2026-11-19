/**
 * صفحه مدیریت کدینگ حساب‌ها — درخت واقعی backend با جست‌وجو/فیلتر،
 * پنل جزئیات، ایجاد/ویرایش/غیرفعال‌سازی و نمای قابل‌ثبت‌ها.
 */
import * as React from 'react';
import { ListTree, Plus, RefreshCw, Search } from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Card } from '@/shared/components/ui/card';
import { Switch } from '@/shared/components/ui/selection-controls';
import { AccountTree } from '@/features/accounts/components/account-tree';
import { AccountDetailsPanel } from '@/features/accounts/components/account-details-panel';
import { AccountFormDialog } from '@/features/accounts/components/account-form-dialog';
import { useAuthStore } from '@/core/auth/auth.store';
import { PERMISSIONS } from '@/core/auth/permissions';
import { getAccountTree, getAccount } from '@/core/accounting/accounts.service';
import { accountLevelLabels, accountNatureLabels } from '@/features/accounts/account-labels';
import type { AccountDto, AccountTreeNodeDto } from '@/core/accounting/accounts.types';

const LEVEL_OPTIONS = Object.entries(accountLevelLabels);
const NATURE_OPTIONS = Object.entries(accountNatureLabels);

export const AccountsPage: React.FC = () => {
  const hasAny = useAuthStore((state) => state.hasAnyPermission);
  const canCreate = hasAny([PERMISSIONS.accountsCreate]);

  const [searchTerm, setSearchTerm] = React.useState('');
  const [debouncedTerm, setDebouncedTerm] = React.useState('');
  const [levelFilter, setLevelFilter] = React.useState('');
  const [natureFilter, setNatureFilter] = React.useState('');
  const [statusFilter, setStatusFilter] = React.useState('');
  const [postableOnly, setPostableOnly] = React.useState(false);
  const [selectedId, setSelectedId] = React.useState<string | null>(null);
  const [formOpen, setFormOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<AccountDto | null>(null);
  const [presetParent, setPresetParent] = React.useState<AccountTreeNodeDto | null>(null);

  const tree = useQuery({
    queryKey: ['accounts', 'tree'],
    queryFn: getAccountTree,
    staleTime: 1000 * 60,
  });

  // debounce سبک برای جست‌وجوی درخت
  React.useEffect(() => {
    const timer = setTimeout(() => setDebouncedTerm(searchTerm), 250);
    return () => clearTimeout(timer);
  }, [searchTerm]);

  const selectedAccount = useQuery({
    queryKey: ['accounts', 'detail', selectedId],
    queryFn: () => getAccount(selectedId!),
    enabled: Boolean(selectedId),
    staleTime: 1000 * 60,
  });

  // فیلترهای سطح/ماهیت/وضعیت روی درخت کامل اعمال می‌شوند (داده از /tree)
  const filteredTree = React.useMemo(() => {
    const applyFilters = (nodes: AccountTreeNodeDto[]): AccountTreeNodeDto[] =>
      nodes
        .filter((node) => {
          if (levelFilter && node.level !== levelFilter) return false;
          if (natureFilter && node.nature !== natureFilter) return false;
          if (statusFilter === 'active' && !node.isActive) return false;
          if (statusFilter === 'inactive' && node.isActive) return false;
          return true;
        })
        .map((node) => ({ ...node, children: applyFilters(node.children) }));
    return applyFilters(tree.data ?? []);
  }, [tree.data, levelFilter, natureFilter, statusFilter]);

  const openCreate = (parent: AccountTreeNodeDto | null) => {
    setEditing(null);
    setPresetParent(parent);
    setFormOpen(true);
  };

  const openEdit = (account: AccountDto) => {
    setPresetParent(null);
    setEditing(account);
    setFormOpen(true);
  };

  return (
    <div className="space-y-4" data-testid="accounts-page">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-lg font-black text-foreground">
            <ListTree className="size-5 text-primary" aria-hidden />
            کدینگ حساب‌ها
          </h1>
          <p className="mt-0.5 text-xs text-muted-foreground">ساختار درختی حساب‌ها، سطح‌بندی backend و تنظیمات تفصیلی هر حساب</p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={() => void tree.refetch()} aria-label="بارگذاری مجدد درخت">
            <RefreshCw className="size-3.5" aria-hidden />
          </Button>
          {canCreate && (
            <Button size="sm" onClick={() => openCreate(null)} data-testid="create-account">
              <Plus className="size-3.5" aria-hidden />
              حساب جدید
            </Button>
          )}
        </div>
      </div>

      {/* نوار فیلتر */}
      <div className="flex flex-wrap items-center gap-2 rounded-lg border border-border/60 bg-surface px-3 py-2.5" data-testid="accounts-filterbar">
        <div className="relative min-w-48 flex-1 sm:max-w-xs">
          <Search className="absolute top-2.5 start-2.5 size-4 text-muted-foreground" aria-hidden />
          <Input
            value={searchTerm}
            onChange={(event) => setSearchTerm(event.target.value)}
            placeholder="جست‌وجوی کد یا نام حساب…"
            className="ps-8"
            aria-label="جست‌وجوی حساب"
            data-testid="accounts-search"
          />
        </div>
        <label className="sr-only" htmlFor="filter-level">فیلتر سطح</label>
        <select
          id="filter-level"
          value={levelFilter}
          onChange={(event) => setLevelFilter(event.target.value)}
          className="h-9 rounded-md border border-border bg-surface px-2 text-xs"
          data-testid="filter-level"
        >
          <option value="">همه سطوح</option>
          {LEVEL_OPTIONS.map(([value, label]) => (
            <option key={value} value={value}>{label}</option>
          ))}
        </select>
        <label className="sr-only" htmlFor="filter-nature">فیلتر ماهیت</label>
        <select
          id="filter-nature"
          value={natureFilter}
          onChange={(event) => setNatureFilter(event.target.value)}
          className="h-9 rounded-md border border-border bg-surface px-2 text-xs"
          data-testid="filter-nature"
        >
          <option value="">همه ماهیت‌ها</option>
          {NATURE_OPTIONS.map(([value, label]) => (
            <option key={value} value={value}>{label}</option>
          ))}
        </select>
        <label className="sr-only" htmlFor="filter-status">فیلتر وضعیت</label>
        <select
          id="filter-status"
          value={statusFilter}
          onChange={(event) => setStatusFilter(event.target.value)}
          className="h-9 rounded-md border border-border bg-surface px-2 text-xs"
          data-testid="filter-status"
        >
          <option value="">همه وضعیت‌ها</option>
          <option value="active">فعال</option>
          <option value="inactive">غیرفعال</option>
        </select>
        <span className="ms-auto">
          <Switch
            checked={postableOnly}
            onCheckedChange={setPostableOnly}
            label="فقط حساب‌های قابل‌ثبت"
            data-testid="postable-only"
          />
        </span>
      </div>

      <div className="grid grid-cols-1 gap-3 lg:grid-cols-5">
        {/* درخت */}
        <Card className="lg:col-span-2 xl:col-span-2">
          <AccountTree
            nodes={filteredTree}
            isLoading={tree.isLoading}
            isError={tree.isError}
            errorDetail="درخت حساب‌ها از backend بارگذاری نشد"
            onRetry={() => void tree.refetch()}
            filterTerm={debouncedTerm}
            expandAll={Boolean(debouncedTerm)}
            postableOnly={postableOnly}
            selectedId={selectedId}
            onSelect={(node) => setSelectedId(node.id)}
            emptyMessage={tree.data && tree.data.length > 0 ? 'با این فیلترها حسابی یافت نشد' : 'کدینگ حساب‌ها هنوز تعریف نشده است'}
          />
        </Card>

        {/* جزئیات */}
        <div className="lg:col-span-3 xl:col-span-3">
          <AccountDetailsPanel
            account={selectedAccount.data ?? null}
            onClose={() => setSelectedId(null)}
            onEdit={openEdit}
            onCreateChild={(parent) => openCreate(parent)}
          />
        </div>
      </div>

      <AccountFormDialog
        open={formOpen}
        onOpenChange={setFormOpen}
        tree={tree.data ?? []}
        isLoadingTree={tree.isLoading}
        editingAccount={editing}
        presetParent={presetParent}
        onSaved={() => void tree.refetch()}
      />
    </div>
  );
};
