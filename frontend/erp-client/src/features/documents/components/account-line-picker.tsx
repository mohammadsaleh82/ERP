/**
 * انتخاب حساب ردیف سند — جست‌وجوی سریع روی حساب‌های قابل‌ثبت واقعی backend
 * (GET /Accounts/postable) با فیلتر کد/نام و ورودی کیبورد؛ خروجی: شناسه + کد کامل + نام + فلاگ‌های تفصیلی.
 */
import * as React from 'react';
import { Search, X } from 'lucide-react';
import { Input } from '@/shared/components/ui/input';
import { Button } from '@/shared/components/ui/button';
import { useQuery } from '@tanstack/react-query';
import { getPostableAccounts } from '@/core/accounting/accounts.service';
import { cn } from '@/shared/utils/cn';
import type { AccountDto } from '@/core/accounting/accounts.types';

export interface AccountLinePickerProps {
  onSelect: (account: AccountDto) => void;
  onClear?: () => void;
  selectedFullCode?: string | null;
  selectedName?: string | null;
  ariaLabel?: string;
  testId?: string;
}

export const AccountLinePicker: React.FC<AccountLinePickerProps> = ({ onSelect, onClear, selectedFullCode, selectedName, ariaLabel = 'انتخاب حساب', testId = 'line-account-picker' }) => {
  const [open, setOpen] = React.useState(false);
  const [term, setTerm] = React.useState('');
  const containerRef = React.useRef<HTMLDivElement>(null);

  const accounts = useQuery({
    queryKey: ['accounts', 'postable'],
    queryFn: getPostableAccounts,
    staleTime: 1000 * 120,
  });

  React.useEffect(() => {
    const handler = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  const filtered = React.useMemo(() => {
    const list = accounts.data ?? [];
    if (!term.trim()) return list.slice(0, 30);
    return list.filter((account) => account.fullCode.includes(term) || account.name.includes(term)).slice(0, 30);
  }, [accounts.data, term]);

  if (selectedFullCode && !open) {
    return (
      <div className="flex min-w-0 items-center gap-1">
        <button
          type="button"
          className="flex min-w-0 flex-1 items-center gap-1.5 rounded-md border border-border bg-surface px-2 py-1.5 text-xs hover:bg-muted"
          onClick={() => setOpen(true)}
          aria-label={`${ariaLabel} — ${selectedName}`}
          data-testid={`${testId}-selected`}
        >
          <span dir="ltr" className="font-code shrink-0 text-[11px] text-muted-foreground">{selectedFullCode}</span>
          <span className="min-w-0 truncate font-medium">{selectedName}</span>
        </button>
        {onClear && (
          <Button type="button" variant="ghost" size="icon" className="size-6 shrink-0" onClick={onClear} aria-label="پاک کردن حساب">
            <X className="size-3.5" aria-hidden />
          </Button>
        )}
      </div>
    );
  }

  return (
    <div ref={containerRef} className="relative min-w-0 flex-1" data-testid={testId}>
      <div className="relative">
        <Search className="absolute top-2 start-2 size-3.5 text-muted-foreground" aria-hidden />
        <Input
          value={term}
          onChange={(event) => setTerm(event.target.value)}
          onFocus={() => setOpen(true)}
          placeholder="جست‌وجوی کد یا نام حساب…"
          className="h-8 ps-7 text-xs"
          aria-label={ariaLabel}
        />
      </div>
      {open && (
        <ul className="absolute z-30 mt-1 max-h-56 w-72 min-w-0 overflow-y-auto rounded-md border border-border bg-surface shadow-lg" role="listbox">
          {accounts.isLoading && <li className="px-3 py-2 text-xs text-muted-foreground">در حال بارگذاری حساب‌های قابل‌ثبت…</li>}
          {!accounts.isLoading && filtered.length === 0 && <li className="px-3 py-2 text-xs text-muted-foreground">حساب قابل‌ثبتی با این عبارت یافت نشد</li>}
          {filtered.map((account) => (
            <li key={account.id} role="option" aria-selected={false}>
              <button
                type="button"
                className={cn('flex w-full items-center gap-2 px-2.5 py-1.5 text-xs hover:bg-muted', !account.isActive && 'text-muted-foreground line-through')}
                onClick={() => {
                  onSelect(account);
                  setOpen(false);
                  setTerm('');
                }}
              >
                <span dir="ltr" className="font-code text-[11px] text-muted-foreground">{account.fullCode}</span>
                <span className="min-w-0 truncate">{account.name}</span>
                {(account.requireCostCenter || account.requireProject || account.requireDetailAccount) && (
                  <span className="ms-auto text-[9px] text-warning" title="برای این حساب تفصیلی الزامی است">تفصیلی</span>
                )}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
};
