/**
 * جست‌وجوی سراسری — فقط روی APIهای واقعی موجود:
 * - اسناد: GET /Documents?documentNumber=  (شماره سند)
 * - حساب‌ها: GET /Accounts?searchTerm=      (کد/نام)
 * - اشخاص و گزارش‌ها: هنوز API جست‌وجو ندارند → بخش Empty با نیاز Backend
 * Debounce ۳۰۰ms + وضعیت‌های Loading/Empty/Error مستقل هر بخش.
 */
import * as React from 'react';
import { useNavigate } from 'react-router-dom';
import { FileText, Loader2, Search, Wallet, Users, BarChart3, SearchX } from 'lucide-react';
import { Popover, PopoverContent, PopoverTrigger } from '@/shared/components/ui/popover';
import { Input } from '@/shared/components/ui/input';
import { Badge } from '@/shared/components/ui/badge';
import { useAuthStore } from '@/core/auth/auth.store';
import { PERMISSIONS } from '@/core/auth/permissions';
import { searchAccounts, searchDocuments } from '@/core/dashboard/dashboard.service';
import type { AccountDto, DocumentDto } from '@/core/dashboard/dashboard.types';
import { digitsToPersian, formatNumberPersian } from '@/shared/utils/number';
import { formatToPersianDate } from '@/shared/utils/date';

type SectionState = 'idle' | 'loading' | 'success' | 'empty' | 'error';

export const GlobalSearch: React.FC = () => {
  const navigate = useNavigate();
  const hasAny = useAuthStore((state) => state.hasAnyPermission);
  const canAccounts = hasAny([PERMISSIONS.accountsRead]);
  const canDocuments = hasAny([PERMISSIONS.documentsRead]);

  const [open, setOpen] = React.useState(false);
  const [term, setTerm] = React.useState('');
  const [debounced, setDebounced] = React.useState('');

  /* فاز ۹ — میان‌بر Ctrl+K از Header (رویداد سراسری) پنل را باز می‌کند */
  React.useEffect(() => {
    const openSearch = () => setOpen(true);
    window.addEventListener('erp:open-global-search', openSearch);
    return () => window.removeEventListener('erp:open-global-search', openSearch);
  }, []);

  const [documents, setDocuments] = React.useState<DocumentDto[]>([]);
  const [accounts, setAccounts] = React.useState<AccountDto[]>([]);
  const [docState, setDocState] = React.useState<SectionState>('idle');
  const [accState, setAccState] = React.useState<SectionState>('idle');

  // Debounce — بدون پرش و درخواست اضافی
  React.useEffect(() => {
    if (!open) return;
    const timer = setTimeout(() => setDebounced(term.trim()), 300);
    return () => clearTimeout(timer);
  }, [term, open]);

  React.useEffect(() => {
    if (!open || debounced.length < 2) {
      setDocState('idle');
      setAccState('idle');
      setDocuments([]);
      setAccounts([]);
      return;
    }

    const numeric = /^\d+$/.test(debounced);

    if (canDocuments && numeric) {
      setDocState('loading');
      searchDocuments({ documentNumber: Number(debounced) })
        .then((result) => {
          setDocuments(result.items);
          setDocState(result.items.length === 0 ? 'empty' : 'success');
        })
        .catch(() => setDocState('error'));
    } else {
      setDocState('idle');
      setDocuments([]);
    }

    if (canAccounts) {
      setAccState('loading');
      searchAccounts(debounced)
        .then((result) => {
          setAccounts(result.items);
          setAccState(result.items.length === 0 ? 'empty' : 'success');
        })
        .catch(() => setAccState('error'));
    } else {
      setAccState('idle');
      setAccounts([]);
    }
  }, [debounced, open, canAccounts, canDocuments]);

  const hasResults = documents.length > 0 || accounts.length > 0;

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <button
          type="button"
          className="flex cursor-pointer items-center gap-2 rounded-md border border-border/70 bg-muted/30 px-2.5 py-1.5 text-xs text-muted-foreground transition-colors hover:bg-muted md:w-56"
          aria-label="جست‌وجوی سراسری"
          data-testid="global-search-trigger"
        >
          <Search className="size-3.5" aria-hidden />
          <span className="hidden md:inline">جست‌وجو (سند، حساب…)</span>
          <kbd className="hidden rounded border bg-background px-1 text-[10px] lg:inline">Ctrl K</kbd>
        </button>
      </PopoverTrigger>
      <PopoverContent className="w-[min(24rem,calc(100vw-1.5rem))] p-3" align="start">
        <Input
          autoFocus
          value={term}
          onChange={(event) => setTerm(event.target.value)}
          placeholder="شماره سند یا کد/نام حساب…"
          aria-label="عبارت جست‌وجو"
          data-testid="global-search-input"
        />

        {debounced.length > 0 && debounced.length < 2 && (
          <p className="mt-3 text-[11px] text-muted-foreground">حداقل ۲ کاراکتر لازم است</p>
        )}

        {/* اسناد */}
        {docState !== 'idle' && (
          <section className="mt-3" data-testid="search-documents">
            <p className="mb-1.5 flex items-center gap-1.5 text-[11px] font-bold text-muted-foreground">
              <FileText className="size-3.5" aria-hidden />
              اسناد
            </p>
            {docState === 'loading' && (
              <p className="flex items-center gap-1.5 text-[11px] text-muted-foreground"><Loader2 className="size-3.5 animate-spin" aria-hidden />در حال جست‌وجو…</p>
            )}
            {docState === 'error' && <p className="text-[11px] text-danger">جست‌وجوی اسناد ناموفق بود</p>}
            {docState === 'empty' && <p className="text-[11px] text-muted-foreground">سندی با این شماره یافت نشد</p>}
            {docState === 'success' && (
              <ul className="max-h-40 space-y-1 overflow-y-auto">
                {documents.map((doc) => (
                  <li key={doc.id}>
                    <button
                      type="button"
                      className="flex w-full cursor-pointer items-center justify-between gap-2 rounded-md px-2 py-1.5 text-start hover:bg-muted"
                      onClick={() => { setOpen(false); navigate('/accounting/documents'); }}
                    >
                      <span className="text-xs">سند {digitsToPersian(doc.documentNumber)}</span>
                      <span className="text-[10px] text-muted-foreground">{formatToPersianDate(doc.documentDate)} · {formatNumberPersian(doc.totalDebit)}</span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </section>
        )}

        {/* حساب‌ها */}
        {accState !== 'idle' && (
          <section className="mt-3" data-testid="search-accounts">
            <p className="mb-1.5 flex items-center gap-1.5 text-[11px] font-bold text-muted-foreground">
              <Wallet className="size-3.5" aria-hidden />
              حساب‌ها
            </p>
            {accState === 'loading' && (
              <p className="flex items-center gap-1.5 text-[11px] text-muted-foreground"><Loader2 className="size-3.5 animate-spin" aria-hidden />در حال جست‌وجو…</p>
            )}
            {accState === 'error' && <p className="text-[11px] text-danger">جست‌وجوی حساب‌ها ناموفق بود</p>}
            {accState === 'empty' && <p className="text-[11px] text-muted-foreground">حسابی با این کد/نام یافت نشد</p>}
            {accState === 'success' && (
              <ul className="max-h-40 space-y-1 overflow-y-auto">
                {accounts.map((account) => (
                  <li key={account.id}>
                    <button
                      type="button"
                      className="flex w-full cursor-pointer items-center justify-between gap-2 rounded-md px-2 py-1.5 text-start hover:bg-muted"
                      onClick={() => { setOpen(false); navigate('/accounting/accounts'); }}
                    >
                      <span dir="ltr" className="font-code text-xs">{account.fullCode}</span>
                      <span className="truncate text-xs text-muted-foreground">{account.name}</span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </section>
        )}

        {/* بخش‌های بدون API */}
        {debounced.length >= 2 && (
          <div className="mt-3 border-t pt-2">
            <div className="flex items-center gap-3 text-[10px] text-muted-foreground">
              <span className="flex items-center gap-1"><Users className="size-3" aria-hidden />اشخاص</span>
              <span className="flex items-center gap-1"><BarChart3 className="size-3" aria-hidden />گزارش‌ها</span>
              <Badge variant="secondary" className="text-[9px]">نیاز Backend: API جست‌وجو موجود نیست</Badge>
            </div>
          </div>
        )}

        {debounced.length >= 2 && !hasResults && docState !== 'loading' && accState !== 'loading' && docState !== 'error' && accState !== 'error' && (
          <p className="mt-2 flex items-center gap-1.5 text-[11px] text-muted-foreground" data-testid="search-no-results">
            <SearchX className="size-3.5" aria-hidden />
            نتیجه‌ای یافت نشد
          </p>
        )}
      </PopoverContent>
    </Popover>
  );
};
