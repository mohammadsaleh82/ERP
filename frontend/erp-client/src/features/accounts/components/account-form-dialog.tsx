/**
 * فرم ایجاد/ویرایش حساب — فیلدها دقیقاً مطابق Create/UpdateAccountRequest و
 * پیام‌های اعتبارسنجی مطابق AccountValidators.cs (کد: رقم فقط ≤20، نام ≤200، توضیحات ≤500).
 * قید Domain: در ویرایش، کد/والد/ماهیت/سطح تغییرناپذیرند و فقط نمایش داده می‌شوند.
 */
import * as React from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Info } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Textarea } from '@/shared/components/ui/textarea';
import { Switch } from '@/shared/components/ui/selection-controls';
import { Tooltip } from '@/shared/components/ui/tooltip';
import { AccountTreePicker } from '@/features/accounts/components/account-tree-picker';
import { levelLabel, natureLabel } from '@/features/accounts/account-labels';
import { createAccount, updateAccount } from '@/core/accounting/accounts.service';
import { toast } from '@/core/notifications/toast';
import type { AccountDto, AccountNature, AccountTreeNodeDto, NormalBalance } from '@/core/accounting/accounts.types';

const createSchema = z.object({
  parentFullCode: z.string(),
  parentAccountId: z.string().nullable(),
  code: z
    .string()
    .min(1, 'کد حساب الزامی است.')
    .max(20, 'کد حساب نمی‌تواند بیش از ۲۰ کاراکتر باشد.')
    .regex(/^[0-9]+$/, 'کد حساب باید صرفاً شامل ارقام عددی باشد.'),
  name: z.string().min(1, 'نام حساب الزامی است.').max(200, 'نام حساب نمی‌تواند بیش از ۲۰۰ کاراکتر باشد.'),
  description: z.string().max(500, 'توضیحات نمی‌تواند بیش از ۵۰۰ کاراکتر باشد.'),
  nature: z.string().nullable(),
  normalBalance: z.string().nullable(),
  allowPosting: z.boolean(),
  requireCostCenter: z.boolean(),
  requireProject: z.boolean(),
  requireDetailAccount: z.boolean(),
});

type CreateFormValues = z.infer<typeof createSchema>;

export interface AccountFormDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  tree: AccountTreeNodeDto[];
  isLoadingTree?: boolean;
  /** ویرایش: حساب هدف */
  editingAccount?: AccountDto | null;
  /** ایجاد با والد از پیش انتخاب‌شده */
  presetParent?: AccountTreeNodeDto | null;
  defaultNature?: AccountNature | null;
  onSaved: (account: AccountDto) => void;
}

const NATURE_OPTIONS: Array<{ value: AccountNature; label: string }> = [
  { value: 'Asset', label: 'دارایی' },
  { value: 'Liability', label: 'بدهی' },
  { value: 'Equity', label: 'سرمایه' },
  { value: 'Revenue', label: 'درآمد' },
  { value: 'Expense', label: 'هزینه' },
];

export const AccountFormDialog: React.FC<AccountFormDialogProps> = ({
  open,
  onOpenChange,
  tree,
  isLoadingTree,
  editingAccount,
  presetParent,
  defaultNature,
  onSaved,
}) => {
  const isEdit = Boolean(editingAccount);
  const [pickerOpen, setPickerOpen] = React.useState(false);

  const {
    register,
    handleSubmit,
    setValue,
    watch,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<CreateFormValues>({
    resolver: zodResolver(createSchema),
    defaultValues: {
      parentFullCode: '',
      parentAccountId: null,
      code: '',
      name: '',
      description: '',
      nature: defaultNature ?? null,
      normalBalance: null,
      allowPosting: true,
      requireCostCenter: false,
      requireProject: false,
      requireDetailAccount: false,
    },
  });

  React.useEffect(() => {
    if (!open) return;
    if (editingAccount) {
      reset({
        parentFullCode: '',
        parentAccountId: editingAccount.parentAccountId,
        code: editingAccount.fullCode,
        name: editingAccount.name,
        description: editingAccount.description ?? '',
        nature: editingAccount.nature,
        normalBalance: editingAccount.normalBalance,
        allowPosting: editingAccount.allowPosting,
        requireCostCenter: editingAccount.requireCostCenter,
        requireProject: editingAccount.requireProject,
        requireDetailAccount: editingAccount.requireDetailAccount,
      });
    } else if (presetParent) {
      reset({
        parentFullCode: presetParent.fullCode,
        parentAccountId: presetParent.id,
        code: '',
        name: '',
        description: '',
        nature: presetParent.nature,
        normalBalance: presetParent.normalBalance,
        allowPosting: true,
        requireCostCenter: false,
        requireProject: false,
        requireDetailAccount: false,
      });
    } else {
      reset({
        parentFullCode: '',
        parentAccountId: null,
        code: '',
        name: '',
        description: '',
        nature: null,
        normalBalance: null,
        allowPosting: true,
        requireCostCenter: false,
        requireProject: false,
        requireDetailAccount: false,
      });
    }
  }, [open, editingAccount, presetParent, reset]);

  const watched = watch();

  const onSubmit = handleSubmit(async (values) => {
    try {
      if (isEdit && editingAccount) {
        // UpdateAccountRequest — فقط فیلدهای مجاز
        const saved = await updateAccount(editingAccount.id, {
          name: values.name,
          description: values.description || null,
          normalBalance: (values.normalBalance as NormalBalance) ?? editingAccount.normalBalance,
          requireCostCenter: values.requireCostCenter,
          requireProject: values.requireProject,
          requireDetailAccount: values.requireDetailAccount,
        });
        toast.success('حساب با موفقیت ویرایش شد', saved.fullCode);
        onSaved(saved);
      } else {
        const saved = await createAccount({
          parentAccountId: values.parentAccountId,
          code: values.code,
          name: values.name,
          description: values.description || null,
          nature: (values.nature as AccountNature | null) ?? null,
          normalBalance: (values.normalBalance as NormalBalance | null) ?? null,
          allowPosting: values.allowPosting,
          requireCostCenter: values.requireCostCenter,
          requireProject: values.requireProject,
          requireDetailAccount: values.requireDetailAccount,
        });
        toast.success('حساب با موفقیت ایجاد شد', saved.fullCode);
        onSaved(saved);
      }
      onOpenChange(false);
    } catch (error) {
      toast.error(isEdit ? 'ویرایش حساب انجام نشد' : 'ایجاد حساب انجام نشد', 'اعتبارسنجی backend را بررسی کنید و دوباره تلاش کنید.');
      throw error;
    }
  });

  return (
    <>
      <Modal
        open={open}
        onOpenChange={onOpenChange}
        title={isEdit ? `ویرایش حساب ${editingAccount?.fullCode}` : 'ایجاد حساب جدید'}
        description={isEdit ? 'کد حساب، والد و ماهیت پس از ایجاد تغییرناپذیرند (قید backend)' : undefined}
        size="lg"
      >
        <form onSubmit={onSubmit} className="space-y-4" data-testid="account-form">
          {!isEdit && (
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              {/* والد */}
              <div className="sm:col-span-2">
                <span className="mb-1 block text-xs font-semibold text-foreground">حساب والد</span>
                <Button type="button" variant="outline" className="w-full justify-between" onClick={() => setPickerOpen(true)} data-testid="pick-parent">
                  <span className="min-w-0 truncate">
                    {watched.parentAccountId ? `${watched.parentFullCode} — ${treeFindName(tree, watched.parentAccountId) ?? ''}` : 'بدون والد (حساب گروه)'}
                  </span>
                  <span className="text-[10px] text-muted-foreground">انتخاب از درخت</span>
                </Button>
                <input type="hidden" {...register('parentAccountId')} />
              </div>

              {/* کد حساب */}
              <div>
                <label htmlFor="acc-code" className="mb-1 block text-xs font-semibold text-foreground">کد حساب *</label>
                <Input
                  id="acc-code"
                  dir="ltr"
                  inputMode="numeric"
                  placeholder="مثلاً 1101"
                  {...register('code')}
                  aria-invalid={Boolean(errors.code)}
                  data-testid="account-code-input"
                />
                {errors.code ? (
                  <p className="mt-1 text-[11px] text-danger" role="alert">{errors.code.message}</p>
                ) : (
                  watched.parentFullCode && (
                    <p className="mt-1 text-[11px] text-muted-foreground">
                      کد کامل پیش‌بینی: <span dir="ltr" className="font-code">{watched.parentFullCode}{watched.code}</span>
                    </p>
                  )
                )}
              </div>

              {/* ماهیت */}
              <div>
                <label htmlFor="acc-nature" className="mb-1 block text-xs font-semibold text-foreground">ماهیت</label>
                <select
                  id="acc-nature"
                  className="h-9 w-full rounded-md border border-border bg-surface px-2 text-sm"
                  {...register('nature')}
                  data-testid="account-nature-select"
                >
                  <option value="">— از والد به ارث می‌رسد —</option>
                  {NATURE_OPTIONS.map((option) => (
                    <option key={option.value} value={option.value}>{option.label}</option>
                  ))}
                </select>
              </div>
            </div>
          )}

          {isEdit && (
            <dl className="grid grid-cols-2 gap-2 rounded-md border border-border/60 bg-muted/30 p-3 text-xs sm:grid-cols-4">
              <div><dt className="text-muted-foreground">کد کامل</dt><dd dir="ltr" className="font-code font-bold">{editingAccount?.fullCode}</dd></div>
              <div><dt className="text-muted-foreground">سطح</dt><dd className="font-bold">{levelLabel(editingAccount?.level ?? '')}</dd></div>
              <div><dt className="text-muted-foreground">ماهیت</dt><dd className="font-bold">{natureLabel(editingAccount?.nature ?? '')}</dd></div>
              <div className="flex items-end">
                <Tooltip content="کد، والد و ماهیت پس از ایجاد تغییرناپذیرند (قید backend)">
                  <span className="flex items-center gap-1 text-[10px] text-muted-foreground"><Info className="size-3" />فیلدهای ثابت</span>
                </Tooltip>
              </div>
            </dl>
          )}

          {/* نام و توضیحات — در هر دو حالت قابل ویرایش */}
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div>
              <label htmlFor="acc-name" className="mb-1 block text-xs font-semibold text-foreground">نام حساب *</label>
              <Input id="acc-name" {...register('name')} aria-invalid={Boolean(errors.name)} data-testid="account-name-input" />
              {errors.name && <p className="mt-1 text-[11px] text-danger" role="alert">{errors.name.message}</p>}
            </div>
            {!isEdit && (
              <div>
                <label htmlFor="acc-normal" className="mb-1 block text-xs font-semibold text-foreground">مانده طبیعی</label>
                <select
                  id="acc-normal"
                  className="h-9 w-full rounded-md border border-border bg-surface px-2 text-sm"
                  {...register('normalBalance')}
                  data-testid="account-normal-select"
                >
                  <option value="">— بر اساس ماهیت —</option>
                  <option value="Debit">بدهکار</option>
                  <option value="Credit">بستانکار</option>
                </select>
              </div>
            )}
          </div>

          <div>
            <label htmlFor="acc-desc" className="mb-1 block text-xs font-semibold text-foreground">توضیحات</label>
            <Textarea id="acc-desc" rows={2} {...register('description')} aria-invalid={Boolean(errors.description)} />
            {errors.description && <p className="mt-1 text-[11px] text-danger" role="alert">{errors.description.message}</p>}
          </div>

          {/* کلیدهای رفتاری */}
          <fieldset className="rounded-md border border-border/60 p-3">
            <legend className="px-1 text-xs font-bold text-foreground">تنظیمات ثبت و تفصیلی</legend>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              {!isEdit && (
                <Switch
                  checked={watched.allowPosting}
                  onCheckedChange={(checked) => setValue('allowPosting', checked)}
                  label="قابل ثبت مستقیم در سند"
                  data-testid="account-allow-posting"
                />
              )}
              <Switch
                checked={watched.requireCostCenter}
                onCheckedChange={(checked) => setValue('requireCostCenter', checked)}
                label="مرکز هزینه الزامی"
                data-testid="account-require-cost-center"
              />
              <Switch
                checked={watched.requireProject}
                onCheckedChange={(checked) => setValue('requireProject', checked)}
                label="پروژه الزامی"
                data-testid="account-require-project"
              />
              <Switch
                checked={watched.requireDetailAccount}
                onCheckedChange={(checked) => setValue('requireDetailAccount', checked)}
                label="حساب تفصیلی الزامی"
                data-testid="account-require-detail"
              />
            </div>
          </fieldset>

          {isEdit && (
            <p className="rounded-md border border-border/60 bg-muted/30 px-3 py-2 text-[11px] text-muted-foreground">
              وضعیت فعلی: {editingAccount?.isActive ? 'فعال' : 'غیرفعال'} · تغییر وضعیت از طریق «غیرفعال‌سازی/فعال‌سازی» در پنل جزئیات
            </p>
          )}

          <div className="flex items-center justify-end gap-2 pt-1">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>انصراف</Button>
            <Button type="submit" disabled={isSubmitting} data-testid="account-form-submit">
              {isSubmitting ? 'در حال ذخیره…' : isEdit ? 'ذخیره تغییرات' : 'ایجاد حساب'}
            </Button>
          </div>
        </form>
      </Modal>

      {!isEdit && (
        <AccountTreePicker
          open={pickerOpen}
          onOpenChange={setPickerOpen}
          tree={tree}
          isLoading={isLoadingTree}
          onSelect={(node) => {
            setValue('parentAccountId', node?.id ?? null);
            setValue('parentFullCode', node?.fullCode ?? '');
            if (node) {
              setValue('nature', node.nature);
              setValue('normalBalance', node.normalBalance);
            }
          }}
        />
      )}
    </>
  );
};

function treeFindName(tree: AccountTreeNodeDto[], id: string): string | null {
  for (const node of tree) {
    if (node.id === id) return node.name;
    const found = treeFindName(node.children, id);
    if (found) return found;
  }
  return null;
}
