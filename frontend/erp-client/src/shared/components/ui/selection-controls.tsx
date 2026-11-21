import * as React from 'react';
import { cn } from '@/shared/utils/cn';

/* ── Checkbox ─────────────────────────────────────────────────────── */
export interface CheckboxProps extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label?: string;
}

export const Checkbox = React.forwardRef<HTMLInputElement, CheckboxProps>(
  ({ className, label, id, ...props }, ref) => {
    const generatedId = React.useId();
    const inputId = id ?? generatedId;
    return (
      <div className="inline-flex items-center gap-2">
        <input
          ref={ref}
          id={inputId}
          type="checkbox"
          className={cn(
            'size-4 cursor-pointer rounded border-input accent-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
            className,
          )}
          {...props}
        />
        {label && (
          <label htmlFor={inputId} className="cursor-pointer select-none text-sm text-foreground">
            {label}
          </label>
        )}
      </div>
    );
  },
);
Checkbox.displayName = 'Checkbox';

/* ── Radio ────────────────────────────────────────────────────────── */
export interface RadioProps extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label?: string;
}

export const Radio = React.forwardRef<HTMLInputElement, RadioProps>(
  ({ className, label, id, ...props }, ref) => {
    const generatedId = React.useId();
    const inputId = id ?? generatedId;
    return (
      <div className="inline-flex items-center gap-2">
        <input
          ref={ref}
          id={inputId}
          type="radio"
          className={cn(
            'size-4 cursor-pointer accent-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
            className,
          )}
          {...props}
        />
        {label && (
          <label htmlFor={inputId} className="cursor-pointer select-none text-sm text-foreground">
            {label}
          </label>
        )}
      </div>
    );
  },
);
Radio.displayName = 'Radio';

/* ── Switch ───────────────────────────────────────────────────────── */
export interface SwitchProps
  extends Omit<React.ButtonHTMLAttributes<HTMLButtonElement>, 'onChange'> {
  checked: boolean;
  onCheckedChange: (checked: boolean) => void;
  label?: string;
}

/** سوییچ روشن/خاموش — role="switch" با پشتیبانی کیبورد از طریق button */
export const Switch = React.forwardRef<HTMLButtonElement, SwitchProps>(
  ({ checked, onCheckedChange, label, className, ...props }, ref) => (
    <div className="inline-flex items-center gap-2">
      <button
        ref={ref}
        type="button"
        role="switch"
        aria-checked={checked}
        onClick={() => onCheckedChange(!checked)}
        className={cn(
          'inline-flex h-5 w-9 shrink-0 cursor-pointer items-center rounded-full border transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50',
          checked ? 'border-primary bg-primary' : 'border-border bg-muted',
          className,
        )}
        {...props}
      >
        <span
          className={cn(
            'block size-4 rounded-full bg-white shadow-sm transition-transform',
            checked ? '-translate-x-4' : 'translate-x-0.5',
          )}
        />
      </button>
      {label && <span className="select-none text-sm text-foreground">{label}</span>}
    </div>
  ),
);
Switch.displayName = 'Switch';
