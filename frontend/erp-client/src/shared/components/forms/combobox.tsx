import * as React from 'react';
import { Check, ChevronDown, Search, X } from 'lucide-react';
import { Popover, PopoverContent, PopoverTrigger } from '@/shared/components/ui/popover';
import { Input } from '@/shared/components/ui/input';
import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import { cn } from '@/shared/utils/cn';

export interface ComboboxOption {
  value: string;
  label: string;
  /** متن کمکی دوم — مثل کد حساب */
  description?: string;
  disabled?: boolean;
}

const useComboboxState = <T extends ComboboxOption>(options: readonly T[]) => {
  const [query, setQuery] = React.useState('');
  const filtered = React.useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return options;
    return options.filter(
      (opt) => opt.label.toLowerCase().includes(q) || (opt.description?.toLowerCase().includes(q) ?? false),
    );
  }, [options, query]);

  return { query, setQuery, filtered };
};

const triggerClass = cn(
  'flex h-9 w-full cursor-pointer items-center justify-between gap-2 rounded-md border border-input bg-surface px-3 text-sm shadow-xs transition-colors',
  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring aria-[invalid=true]:border-danger',
  'disabled:cursor-not-allowed disabled:opacity-50',
);

/* ── SearchableSelect (تک‌انتخابی با جستجو) ────────────────────────── */
export interface SearchableSelectProps {
  options: readonly ComboboxOption[];
  value: string | null;
  onChange: (value: string | null) => void;
  placeholder?: string;
  emptyMessage?: string;
  disabled?: boolean;
  'aria-label'?: string;
}

export const SearchableSelect: React.FC<SearchableSelectProps> = ({
  options,
  value,
  onChange,
  placeholder = 'انتخاب کنید…',
  emptyMessage = 'موردی یافت نشد',
  disabled,
  'aria-label': ariaLabel,
}) => {
  const { query, setQuery, filtered } = useComboboxState(options);
  const selected = options.find((opt) => opt.value === value) ?? null;

  return (
    <Popover>
      <PopoverTrigger asChild>
        <button type="button" disabled={disabled} aria-label={ariaLabel ?? placeholder} className={triggerClass}>
          <span className={cn('truncate', !selected && 'text-muted-foreground')}>
            {selected ? selected.label : placeholder}
          </span>
          <ChevronDown className="size-4 shrink-0 text-muted-foreground" aria-hidden />
        </button>
      </PopoverTrigger>
      <PopoverContent className="p-0" align="start">
        <div className="border-b p-2">
          <div className="relative">
            <Search className="absolute top-1/2 right-2.5 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden />
            <Input
              autoFocus
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="جستجو…"
              className="h-8 pr-8"
            />
          </div>
        </div>
        <div className="thin-scroll max-h-56 overflow-y-auto p-1" role="listbox" aria-label={ariaLabel ?? 'گزینه‌ها'}>
          {filtered.length === 0 ? (
            <p className="p-4 text-center text-xs text-muted-foreground">{emptyMessage}</p>
          ) : (
            filtered.map((opt) => (
              <button
                key={opt.value}
                type="button"
                role="option"
                aria-selected={opt.value === value}
                disabled={opt.disabled}
                onClick={() => {
                  onChange(opt.value);
                  setQuery('');
                }}
                className={cn(
                  'flex w-full cursor-pointer flex-col items-start gap-0.5 rounded-md px-2.5 py-2 text-start text-sm transition-colors hover:bg-muted',
                  opt.value === value && 'bg-primary-soft text-primary',
                  opt.disabled && 'cursor-not-allowed opacity-50',
                )}
              >
                <span className="font-medium">{opt.label}</span>
                {opt.description && <span className="font-code text-xs text-muted-foreground">{opt.description}</span>}
              </button>
            ))
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
};

/* ── MultiSelect ──────────────────────────────────────────────────── */
export interface MultiSelectProps {
  options: readonly ComboboxOption[];
  value: readonly string[];
  onChange: (value: string[]) => void;
  placeholder?: string;
  disabled?: boolean;
  'aria-label'?: string;
}

export const MultiSelect: React.FC<MultiSelectProps> = ({
  options,
  value,
  onChange,
  placeholder = 'انتخاب موارد…',
  disabled,
  'aria-label': ariaLabel,
}) => {
  const { query, setQuery, filtered } = useComboboxState(options);
  const selectedOptions = options.filter((opt) => value.includes(opt.value));

  const toggle = (optionValue: string) => {
    onChange(value.includes(optionValue) ? value.filter((v) => v !== optionValue) : [...value, optionValue]);
  };

  return (
    <div className="space-y-2">
      <Popover>
        <PopoverTrigger asChild>
          <button type="button" disabled={disabled} aria-label={ariaLabel ?? placeholder} className={triggerClass}>
            <span className={cn('truncate', value.length === 0 && 'text-muted-foreground')}>
              {value.length === 0 ? placeholder : `${selectedOptions.length} مورد انتخاب شده`}
            </span>
            <ChevronDown className="size-4 shrink-0 text-muted-foreground" aria-hidden />
          </button>
        </PopoverTrigger>
        <PopoverContent className="p-0" align="start">
          <div className="border-b p-2">
            <div className="relative">
              <Search className="absolute top-1/2 right-2.5 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden />
              <Input
                autoFocus
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="جستجو…"
                className="h-8 pr-8"
              />
            </div>
          </div>
          <div className="thin-scroll max-h-56 overflow-y-auto p-1" role="listbox" aria-multiselectable="true">
            {filtered.length === 0 ? (
              <p className="p-4 text-center text-xs text-muted-foreground">موردی یافت نشد</p>
            ) : (
              filtered.map((opt) => (
                <button
                  key={opt.value}
                  type="button"
                  role="option"
                  aria-selected={value.includes(opt.value)}
                  disabled={opt.disabled}
                  onClick={() => toggle(opt.value)}
                  className={cn(
                    'flex w-full cursor-pointer items-center justify-between gap-2 rounded-md px-2.5 py-2 text-start text-sm transition-colors hover:bg-muted',
                    opt.disabled && 'cursor-not-allowed opacity-50',
                  )}
                >
                  <span>{opt.label}</span>
                  {value.includes(opt.value) && <Check className="size-4 text-primary" aria-hidden />}
                </button>
              ))
            )}
          </div>
        </PopoverContent>
      </Popover>
      {selectedOptions.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {selectedOptions.map((opt) => (
            <Badge key={opt.value} variant="secondary" className="gap-1">
              {opt.label}
              <button
                type="button"
                aria-label={`حذف ${opt.label}`}
                className="cursor-pointer text-muted-foreground hover:text-danger"
                onClick={() => toggle(opt.value)}
              >
                <X className="size-3" aria-hidden />
              </button>
            </Badge>
          ))}
          <Button variant="ghost" size="sm" className="h-6 px-2 text-xs" onClick={() => onChange([])}>
            پاک کردن همه
          </Button>
        </div>
      )}
    </div>
  );
};

/* ── Combobox (جستجو + ساخت مقدار جدید) ───────────────────────────── */
export interface ComboboxProps {
  options: readonly ComboboxOption[];
  value: string | null;
  onChange: (value: string | null) => void;
  placeholder?: string;
  /** اجازه انتخاب مقدار جدید تایپ‌شده */
  allowCreate?: boolean;
  createLabel?: string;
  disabled?: boolean;
  'aria-label'?: string;
}

export const Combobox: React.FC<ComboboxProps> = ({
  options,
  value,
  onChange,
  placeholder = 'جستجو یا تایپ کنید…',
  allowCreate = false,
  createLabel = 'ایجاد',
  disabled,
  'aria-label': ariaLabel,
}) => {
  const { query, setQuery, filtered } = useComboboxState(options);
  const selected = options.find((opt) => opt.value === value) ?? null;
  const exactMatch = options.some((opt) => opt.label === query.trim());

  return (
    <Popover>
      <PopoverTrigger asChild>
        <div className="relative w-full">
          <Input
            disabled={disabled}
            aria-label={ariaLabel ?? placeholder}
            value={selected ? selected.label : query}
            onChange={(e) => {
              setQuery(e.target.value);
              onChange(null);
            }}
            placeholder={placeholder}
            className="pl-8"
          />
          <ChevronDown className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden />
        </div>
      </PopoverTrigger>
      <PopoverContent className="p-1" align="start">
        <div className="thin-scroll max-h-56 overflow-y-auto" role="listbox">
          {filtered.length === 0 && !allowCreate ? (
            <p className="p-4 text-center text-xs text-muted-foreground">موردی یافت نشد</p>
          ) : (
            filtered.map((opt) => (
              <button
                key={opt.value}
                type="button"
                role="option"
                aria-selected={opt.value === value}
                disabled={opt.disabled}
                onClick={() => {
                  onChange(opt.value);
                  setQuery('');
                }}
                className={cn(
                  'flex w-full cursor-pointer flex-col items-start gap-0.5 rounded-md px-2.5 py-2 text-start text-sm transition-colors hover:bg-muted',
                  opt.value === value && 'bg-primary-soft text-primary',
                  opt.disabled && 'cursor-not-allowed opacity-50',
                )}
              >
                <span className="font-medium">{opt.label}</span>
                {opt.description && <span className="font-code text-xs text-muted-foreground">{opt.description}</span>}
              </button>
            ))
          )}
          {allowCreate && !exactMatch && query.trim() !== '' && (
            <button
              type="button"
              onClick={() => {
                onChange(query.trim());
                setQuery('');
              }}
              className="flex w-full cursor-pointer items-center gap-1.5 rounded-md px-2.5 py-2 text-start text-sm text-primary transition-colors hover:bg-muted"
            >
              <Check className="size-3.5" aria-hidden />
              {createLabel}: «{query.trim()}»
            </button>
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
};
