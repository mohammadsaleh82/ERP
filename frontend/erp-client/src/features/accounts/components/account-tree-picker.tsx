/** انتخابگر حساب از درخت — برای انتخاب حساب والد در فرم ایجاد */
import * as React from 'react';
import { Search } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { AccountTree } from '@/features/accounts/components/account-tree';
import type { AccountTreeNodeDto } from '@/core/accounting/accounts.types';

export interface AccountTreePickerProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  tree: AccountTreeNodeDto[];
  isLoading?: boolean;
  /** والد انتخاب‌شده — null = ریشه */
  onSelect: (node: AccountTreeNodeDto | null) => void;
  title?: string;
  /** اجازه انتخاب ریشه (بدون والد) */
  allowRoot?: boolean;
}

export const AccountTreePicker: React.FC<AccountTreePickerProps> = ({
  open,
  onOpenChange,
  tree,
  isLoading,
  onSelect,
  title = 'انتخاب حساب والد',
  allowRoot = true,
}) => {
  const [term, setTerm] = React.useState('');
  const [picked, setPicked] = React.useState<AccountTreeNodeDto | null>(null);

  const handleConfirm = () => {
    onSelect(picked);
    onOpenChange(false);
    setPicked(null);
    setTerm('');
  };

  return (
    <Modal open={open} onOpenChange={onOpenChange} title={title} size="md">
      <div className="space-y-3">
        <div className="relative">
          <Search className="absolute top-2.5 start-2.5 size-4 text-muted-foreground" aria-hidden />
          <Input
            value={term}
            onChange={(event) => setTerm(event.target.value)}
            placeholder="جست‌وجوی کد یا نام حساب…"
            aria-label="جست‌وجوی حساب برای انتخاب"
            className="ps-8"
            data-testid="tree-picker-search"
          />
        </div>
        <div className="max-h-80 overflow-y-auto rounded-md border border-border/60">
          <AccountTree nodes={tree} isLoading={isLoading} filterTerm={term} expandAll onSelect={setPicked} selectedId={picked?.id} />
        </div>
        <div className="flex items-center justify-between gap-2">
          {allowRoot ? (
            <Button variant="ghost" size="sm" onClick={() => { onSelect(null); onOpenChange(false); }}>
              بدون والد (حساب گروه)
            </Button>
          ) : <span />}
          <div className="flex items-center gap-2">
            <span className="min-w-0 truncate text-xs text-muted-foreground">
              {picked ? `${picked.fullCode} — ${picked.name}` : 'حسابی انتخاب نشده'}
            </span>
            <Button size="sm" onClick={handleConfirm} disabled={!picked} data-testid="tree-picker-confirm">
              تأیید
            </Button>
          </div>
        </div>
      </div>
    </Modal>
  );
};
