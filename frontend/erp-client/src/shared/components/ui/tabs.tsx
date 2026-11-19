import * as React from 'react';
import { cn } from '@/shared/utils/cn';

export interface TabItem {
  value: string;
  label: string;
  icon?: React.ReactNode;
  disabled?: boolean;
}

export interface TabsProps {
  items: TabItem[];
  /** کنترل‌شده */
  value?: string;
  onValueChange?: (value: string) => void;
  defaultValue?: string;
  className?: string;
  'aria-label'?: string;
}

/**
 * تب ساده دسترس‌پذیر — کنترل‌شده یا uncontrolled.
 * نقش‌ها: role="tablist"/"tab" + aria-selected برای ناوبری کیبورد.
 */
export const Tabs: React.FC<TabsProps> = ({ items, value, onValueChange, defaultValue, className, 'aria-label': ariaLabel }) => {
  const [internal, setInternal] = React.useState(defaultValue ?? items[0]?.value);
  const active = value ?? internal;

  const handleSelect = (itemValue: string) => {
    setInternal(itemValue);
    onValueChange?.(itemValue);
  };

  return (
    <div role="tablist" aria-label={ariaLabel} className={cn('inline-flex items-center gap-1 rounded-lg bg-muted p-1', className)}>
      {items.map((item) => {
        const isActive = item.value === active;
        return (
          <button
            key={item.value}
            type="button"
            role="tab"
            aria-selected={isActive}
            disabled={item.disabled}
            onClick={() => handleSelect(item.value)}
            className={cn(
              'inline-flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50',
              isActive ? 'bg-surface text-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground',
            )}
          >
            {item.icon}
            {item.label}
          </button>
        );
      })}
    </div>
  );
};
