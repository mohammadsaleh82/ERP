/**
 * محافظ خروج از فرم با تغییرات ذخیره‌نشده (فاز ۹ — UX):
 * ۱) reload/بستن تب → beforeunload با پیام پیش‌فرض مرورگر.
 * ۲) ناوبری درون‌برنامه‌ای (react-router navigate/back) → رهگیری
 *    window.history.pushState/replaceState در مدت dirty و نمایش ConfirmDialog؛
 *    در تأیید کاربر همان ناوبری ادامه می‌یابد. در تست‌ها (MemoryRouter) رهگیری
 *    window.history کاری نمی‌کند و hook بی‌اثر می‌ماند — بدون شکستن تست‌ها.
 */
import * as React from 'react';
import { ConfirmDialog } from '@/shared/components/ui/confirm-dialog';

export interface UnsavedChangesGuardOptions {
  /** فرم تغییرات ذخیره‌نشده دارد؟ */
  dirty: boolean;
  /** عنوان و متن دیالوگ تأیید */
  title?: string;
  message?: string;
  /** اجرای ناوبری معلق پس از تأیید (اختیاری) */
  onConfirmNavigation?: () => void;
}

export const useUnsavedChangesGuard = ({ dirty, title = 'خروج با تغییرات ذخیره‌نشده', message = 'تغییرات فرم هنوز ذخیره نشده است. با خروج، همه تغییرات از بین می‌رود.', onConfirmNavigation }: UnsavedChangesGuardOptions) => {
  const dirtyRef = React.useRef(dirty);
  dirtyRef.current = dirty;
  const [dialogOpen, setDialogOpen] = React.useState(false);
  const pendingRef = React.useRef<(() => void) | null>(null);
  const restoringRef = React.useRef(false);

  const release = React.useCallback(() => {
    pendingRef.current = null;
    setDialogOpen(false);
  }, []);

  /* ۱) reload/بستن تب */
  React.useEffect(() => {
    if (!dirty) return;
    const handler = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', handler);
    return () => window.removeEventListener('beforeunload', handler);
  }, [dirty]);

  /* ۲) ناوبری درون‌برنامه‌ای — رهگیری pushState/replaceState تا پاک‌شدن dirty */
  React.useEffect(() => {
    const guarded = () => dirtyRef.current && !restoringRef.current;
    if (!dirty) return;
    const originalPush = window.history.pushState.bind(window.history);
    const originalReplace = window.history.replaceState.bind(window.history);
    const intercept = (original: typeof window.history.pushState) =>
      ((...args: Parameters<typeof window.history.pushState>) => {
        if (guarded()) {
          pendingRef.current = () => original(...args);
          setDialogOpen(true);
          return;
        }
        original(...args);
      }) as typeof window.history.pushState;
    window.history.pushState = intercept(originalPush);
    window.history.replaceState = intercept(originalReplace);
    return () => {
      window.history.pushState = originalPush;
      window.history.replaceState = originalReplace;
    };
  }, [dirty]);

  const confirmNavigation = React.useCallback(() => {
    restoringRef.current = true;
    const pending = pendingRef.current;
    release();
    onConfirmNavigation?.();
    pending?.();
    // بعد از یک تیک dirty فرم صفر می‌شود (پس از ذخیره) → رهگیری برداشته می‌شود
    setTimeout(() => { restoringRef.current = false; }, 0);
  }, [release, onConfirmNavigation]);

  const cancelNavigation = React.useCallback(() => release(), [release]);

  const guardDialog = (
    <ConfirmDialog
      open={dialogOpen}
      onOpenChange={(open) => { if (!open) cancelNavigation(); }}
      title={title}
      description={message}
      actionLabel='خروج بدون ذخیره'
      cancelLabel='ماندن در فرم'
      onConfirm={confirmNavigation}
    />
  );

  return { guardDialog };
};
