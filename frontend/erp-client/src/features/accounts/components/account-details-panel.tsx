/**
 * پنل جزئیات حساب انتخاب‌شده — breadcrumb والد، فیلدهای واقعی، فرزندان،
 * عملیات ویرایش/غیرفعال‌سازی/فعال‌سازی/حذف با نمایش محدودیت‌های مالی.
 * قیدهای حذف backend: CannotDeleteSystemAccount / CannotDeleteAccountWithChildren /
 * CannotDeleteAccountWithFinancialActivity.
 */
import * as React from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowLeft, FileStack, Lock, Pencil, PowerOff, Trash2, Unlock } from 'lucide-react';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import { Card, CardContent, CardHeader } from '@/shared/components/ui/card';
import { ConfirmDialog } from '@/shared/components/ui/confirm-dialog';
import { EmptyState } from '@/shared/components/feedback/empty-state';
import { useAuthStore } from '@/core/auth/auth.store';
import { PERMISSIONS } from '@/core/auth/permissions';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getAccountBreadcrumb, getAccountChildren, deactivateAccount, activateAccount, deleteAccount } from '@/core/accounting/accounts.service';
import { levelLabel, natureLabel, normalBalanceLabel } from '@/features/accounts/account-labels';
import { toast } from '@/core/notifications/toast';
import { ApiError } from '@/core/errors/problem-details';

/** پیام فارسی خطای backend (ApiError.message) یا fallback محلی */
const getApiErrorMessage = (error: unknown, fallback?: string): string =>
  error instanceof ApiError ? error.message : fallback ?? 'خطای غیرمنتظره رخ داد';
import type { AccountDto, AccountTreeNodeDto } from '@/core/accounting/accounts.types';

export interface AccountDetailsPanelProps {
  account: AccountDto | null;
  onClose?: () => void;
  onEdit: (account: AccountDto) => void;
  onCreateChild: (parent: AccountTreeNodeDto) => void;
}

export const AccountDetailsPanel: React.FC<AccountDetailsPanelProps> = ({ account, onClose, onEdit, onCreateChild }) => {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const hasAny = useAuthStore((state) => state.hasAnyPermission);
  const canUpdate = hasAny([PERMISSIONS.accountsUpdate]);
  const canDeactivate = hasAny([PERMISSIONS.accountsDeactivate]);
  const canDelete = hasAny([PERMISSIONS.accountsDelete]);
  const canCreate = hasAny([PERMISSIONS.accountsCreate]);
  const canViewLedger = hasAny([PERMISSIONS.ledgerRead]);

  const [confirmAction, setConfirmAction] = React.useState<'deactivate' | 'delete' | null>(null);

  const breadcrumb = useQuery({
    queryKey: ['account-breadcrumb', account?.id],
    queryFn: () => getAccountBreadcrumb(account!.id),
    enabled: Boolean(account?.id),
    staleTime: 1000 * 60,
  });

  const children = useQuery({
    queryKey: ['account-children', account?.id],
    queryFn: () => getAccountChildren(account!.id),
    enabled: Boolean(account?.id) && Boolean(account?.hasChildren),
    staleTime: 1000 * 60,
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ['accounts'] });
    void queryClient.invalidateQueries({ queryKey: ['account-children'] });
  };

  const deactivateMutation = useMutation({
    mutationFn: () => deactivateAccount(account!.id),
    onSuccess: () => {
      toast.success('حساب غیرفعال شد', `${account!.fullCode} دیگر در اسناد جدید قابل انتخاب نیست.`);
      invalidate();
      setConfirmAction(null);
    },
    onError: (error) => {
      toast.error('غیرفعال‌سازی انجام نشد', getApiErrorMessage(error, 'محدودیت backend این عملیات را متوقف کرد.'));
      setConfirmAction(null);
    },
  });

  const activateMutation = useMutation({
    mutationFn: () => activateAccount(account!.id),
    onSuccess: () => {
      toast.success('حساب فعال شد', account!.fullCode);
      invalidate();
      setConfirmAction(null);
    },
    onError: (error) => {
      toast.error('فعال‌سازی انجام نشد', getApiErrorMessage(error));
      setConfirmAction(null);
    },
  });

  const deleteMutation = useMutation({
    mutationFn: () => deleteAccount(account!.id),
    onSuccess: () => {
      toast.success('حساب حذف شد', `${account!.fullCode} — فقط حساب بدون فرزند و بدون گردش مالی قابل حذف است.`);
      invalidate();
      onClose?.();
      setConfirmAction(null);
    },
    onError: (error) => {
      toast.error('حذف حساب انجام نشد', getApiErrorMessage(error, 'این حساب یا سیستمی است، یا فرزند دارد، یا گردش مالی ثبت‌شده.'));
      setConfirmAction(null);
    },
  });

  if (!account) {
    return (
      <Card className="h-full">
        <CardContent className="flex h-full items-center justify-center p-8">
          <EmptyState title="حسابی انتخاب نشده" description="از درخت سمت راست یک حساب انتخاب کنید تا جزئیات آن نمایش داده شود." />
        </CardContent>
      </Card>
    );
  }

  // محدودیت‌های واقعی حذف — پیش از کلیک به کاربر نشان داده می‌شود
  const deleteConstraints: string[] = [];
  if (account.isSystemAccount) deleteConstraints.push('حساب سیستمی است — حذف ممنوع (CannotDeleteSystemAccount)');
  if (account.hasChildren) deleteConstraints.push('دارای حساب فرزند است — ابتدا فرزندان را حذف کنید (CannotDeleteAccountWithChildren)');
  deleteConstraints.push('در صورت وجود گردش مالی در اسناد، حذف پذیرفته نمی‌شود (CannotDeleteAccountWithFinancialActivity)');

  const childParentNode: AccountTreeNodeDto = {
    id: account.id,
    parentAccountId: account.parentAccountId,
    code: account.code,
    fullCode: account.fullCode,
    name: account.name,
    level: account.level,
    nature: account.nature,
    normalBalance: account.normalBalance,
    isActive: account.isActive,
    allowPosting: account.allowPosting,
    children: [],
  };

  return (
    <Card className="h-full" data-testid="account-details">
      <CardHeader className="flex flex-row items-center justify-between gap-2 border-b border-border/60 pb-3">
        <div className="min-w-0">
          <p className="flex items-center gap-2 text-sm font-bold text-foreground">
            <span dir="ltr" className="font-code">{account.fullCode}</span>
            {account.name}
          </p>
          <p className="mt-0.5 flex flex-wrap items-center gap-1">
            <Badge variant="outline" className="text-[10px]">{levelLabel(account.level)}</Badge>
            <Badge variant="secondary" className="text-[10px]">{natureLabel(account.nature)}</Badge>
            <Badge variant="secondary" className="text-[10px]">مانده {normalBalanceLabel(account.normalBalance)}</Badge>
            {account.isSystemAccount && <Badge variant="warning" className="text-[10px]">سیستمی</Badge>}
            {account.isActive ? (
              <Badge variant="success" className="text-[10px]">فعال</Badge>
            ) : (
              <Badge variant="destructive" className="text-[10px]">غیرفعال</Badge>
            )}
            <Badge variant={account.allowPosting ? 'success' : 'secondary'} className="text-[10px]">
              {account.allowPosting ? 'قابل‌ثبت مستقیم' : 'تجمیعی (غیرقابل‌ثبت)'}
            </Badge>
          </p>
        </div>
        <div className="flex items-center gap-1">
          {canUpdate && (
            <Button variant="outline" size="sm" onClick={() => onEdit(account)} data-testid="edit-account">
              <Pencil className="size-3.5" aria-hidden />
              ویرایش
            </Button>
          )}
          {onClose && (
            <Button variant="ghost" size="icon" className="size-7" onClick={onClose} aria-label="بستن جزئیات">
              <ArrowLeft className="size-4" aria-hidden />
            </Button>
          )}
        </div>
      </CardHeader>
      <CardContent className="space-y-4 p-4">
        {/* breadcrumb والد */}
        <div className="flex flex-wrap items-center gap-1 text-[11px] text-muted-foreground" data-testid="account-breadcrumb">
          {breadcrumb.isLoading && <span className="animate-pulse">…</span>}
          {breadcrumb.data?.map((item, index) => (
            <React.Fragment key={item.id}>
              {index > 0 && <span aria-hidden>/</span>}
              <span dir="ltr" className="font-code">{item.fullCode}</span>
              <span>{item.name}</span>
            </React.Fragment>
          ))}
          {breadcrumb.data && breadcrumb.data.length === 0 && <span>حساب ریشه</span>}
        </div>

        {account.description && <p className="rounded-md border border-border/60 bg-muted/30 px-3 py-2 text-xs text-muted-foreground">{account.description}</p>}

        {/* تنظیمات تفصیلی */}
        <dl className="grid grid-cols-2 gap-2 text-xs sm:grid-cols-3">
          <div className="rounded-md border border-border/60 p-2.5"><dt className="text-muted-foreground">مرکز هزینه</dt><dd className="mt-0.5 font-semibold">{account.requireCostCenter ? 'الزامی' : 'مورد نیاز نیست'}</dd></div>
          <div className="rounded-md border border-border/60 p-2.5"><dt className="text-muted-foreground">پروژه</dt><dd className="mt-0.5 font-semibold">{account.requireProject ? 'الزامی' : 'مورد نیاز نیست'}</dd></div>
          <div className="rounded-md border border-border/60 p-2.5"><dt className="text-muted-foreground">حساب تفصیلی</dt><dd className="mt-0.5 font-semibold">{account.requireDetailAccount ? 'الزامی' : 'مورد نیاز نیست'}</dd></div>
        </dl>

        {/* فرزندان */}
        {account.hasChildren && (
          <section>
            <p className="mb-1.5 text-xs font-bold text-foreground">حساب‌های فرزند</p>
            {children.isLoading && <p className="text-[11px] text-muted-foreground">در حال بارگذاری…</p>}
            <ul className="grid grid-cols-1 gap-1.5 sm:grid-cols-2">
              {children.data?.map((child) => (
                <li key={child.id} className="flex items-center justify-between gap-2 rounded-md border border-border/60 px-2.5 py-1.5 text-xs">
                  <span className="min-w-0"><span dir="ltr" className="font-code text-muted-foreground">{child.fullCode}</span> {child.name}</span>
                  {!child.isActive && <Badge variant="secondary" className="text-[9px]">غیرفعال</Badge>}
                </li>
              ))}
            </ul>
          </section>
        )}

        {/* محدودیت‌های واقعی حذف — همیشه برای مجوزدار نمایان */}
        {canDelete && !account.isSystemAccount && (
          <div className="rounded-md border border-warning/30 bg-warning/5 px-3 py-2" data-testid="delete-constraints">
            <p className="text-[11px] font-bold text-foreground">محدودیت‌های حذف (قیدهای backend):</p>
            <ul className="mt-1 space-y-0.5 text-[11px] text-muted-foreground">
              {deleteConstraints.map((constraint) => (
                <li key={constraint} className="flex items-start gap-1.5">
                  <Lock className="mt-0.5 size-3 shrink-0 text-warning" aria-hidden />
                  {constraint}
                </li>
              ))}
            </ul>
          </div>
        )}

        {/* عملیات */}
        <div className="flex flex-wrap items-center gap-2 border-t border-border/60 pt-3">
          {canCreate && (
            <Button variant="outline" size="sm" onClick={() => onCreateChild(childParentNode)} data-testid="create-child">
              ایجاد زیرحساب
            </Button>
          )}
          {canViewLedger && (
            <Button variant="outline" size="sm" onClick={() => navigate('/reporting')}>
              <FileStack className="size-3.5" aria-hidden />
              گردش و کارت حساب
            </Button>
          )}
          {canDeactivate && account.isActive && (
            <Button variant="outline" size="sm" className="text-warning" onClick={() => setConfirmAction('deactivate')} data-testid="deactivate-account">
              <PowerOff className="size-3.5" aria-hidden />
              غیرفعال‌سازی
            </Button>
          )}
          {canDeactivate && !account.isActive && (
            <Button variant="outline" size="sm" onClick={() => setConfirmAction('deactivate')} data-testid="activate-account">
              <Unlock className="size-3.5" aria-hidden />
              فعال‌سازی
            </Button>
          )}
          {canDelete && !account.isSystemAccount && (
            <Button variant="outline" size="sm" className="text-danger" onClick={() => setConfirmAction('delete')} disabled={account.hasChildren} data-testid="delete-account">
              <Trash2 className="size-3.5" aria-hidden />
              حذف
            </Button>
          )}
        </div>
      </CardContent>

      {/* تأیید غیرفعال/فعال */}
      <ConfirmDialog
        open={confirmAction === 'deactivate'}
        onOpenChange={(open) => !open && setConfirmAction(null)}
        title={account.isActive ? 'غیرفعال‌سازی حساب' : 'فعال‌سازی حساب'}
        description={
          account.isActive
            ? `حساب «${account.name}» غیرفعال می‌شود: در اسناد جدید قابل انتخاب نخواهد بود اما مانده و گردش قبلی محفوظ می‌ماند.`
            : `حساب «${account.name}» مجدداً فعال می‌شود و در انتخاب حساب اسناد ظاهر خواهد شد.`
        }
        actionLabel={account.isActive ? 'غیرفعال کن' : 'فعال کن'}
        onConfirm={() => (account.isActive ? deactivateMutation.mutate() : activateMutation.mutate())}
      />

      {/* تأیید حذف با نمایش محدودیت‌های مالی */}
      <ConfirmDialog
        open={confirmAction === 'delete'}
        onOpenChange={(open) => !open && setConfirmAction(null)}
        title="حذف حساب"
        description={
          <span className="block space-y-1">
            <span>حذف فقط برای حساب بدون فرزند و بدون گردش مالی ممکن است. محدودیت‌ها:</span>
            <ul className="mt-1 space-y-0.5 text-[11px] text-muted-foreground">
              {deleteConstraints.map((constraint) => (
                <li key={constraint} className="flex items-start gap-1.5">
                  <Lock className="mt-0.5 size-3 shrink-0 text-warning" aria-hidden />
                  {constraint}
                </li>
              ))}
            </ul>
          </span>
        }
        actionLabel="حذف قطعی"
        onConfirm={() => deleteMutation.mutate()}
      />
    </Card>
  );
};
