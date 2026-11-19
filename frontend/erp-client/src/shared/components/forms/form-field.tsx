import * as React from 'react';
import { cn } from '@/shared/utils/cn';

export interface FormFieldProps {
  /** شناسه فیلد — با htmlFor به کنترل وصل می‌شود */
  id: string;
  label: string;
  /** ستاره الزام — نمایشی؛ اعتبارسنجی با Zod انجام می‌شود */
  required?: boolean;
  /** پیام خطای فیلد؛ اگر موجود باشد کنترل aria-invalid می‌گیرد */
  error?: string;
  /** متن راهنمای ثابت زیر فیلد */
  hint?: string;
  children: React.ReactNode;
  className?: string;
}

/**
 * بسته‌بندی استاندارد فیلد فرم: Label + کنترل + خطا + راهنما.
 * اصل UX: خطا نزدیک همان فیلد و با role="alert" خوانده می‌شود.
 */
export const FormField: React.FC<FormFieldProps> = ({ id, label, required, error, hint, children, className }) => (
  <div className={cn('flex flex-col gap-1.5', className)}>
    <label htmlFor={id} className="text-sm font-medium text-foreground">
      {label}
      {required && (
        <span className="text-danger" aria-hidden>
          {' '}*
        </span>
      )}
    </label>
    {children}
    {error ? (
      <p role="alert" id={`${id}-error`} className="text-xs font-medium text-danger">
        {error}
      </p>
    ) : hint ? (
      <p id={`${id}-hint`} className="text-xs text-muted-foreground">
        {hint}
      </p>
    ) : null}
  </div>
);
