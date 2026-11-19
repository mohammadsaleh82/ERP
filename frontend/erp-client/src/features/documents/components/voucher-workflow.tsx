/**
 * عملیات گردش کار سند — Submit/Approve/Reject/Post/Reverse با وضعیت‌های واقعی backend.
 * هر عملیات: تأیید صریح، جلوگیری از کلیک تکراری (mutation.isPending)، مدیریت خطا با
 * پیام فارسی ApiError، به‌روزرسانی وضعیت پس از پاسخ موفق و invalidate لیست.
 */
import * as React from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Ban, CheckCheck, FileSignature, Send, Trash2, Undo2 } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Modal } from '@/shared/components/ui/modal';
import { ConfirmDialog } from '@/shared/components/ui/confirm-dialog';
import { Input } from '@/shared/components/ui/input';
import { Textarea } from '@/shared/components/ui/textarea';
import { approveDocument, deleteDocument, postDocument, rejectDocument, reverseDocument, submitDocument } from '@/core/accounting/documents.service';
import { ApiError } from '@/core/errors/problem-details';
import { toast } from '@/core/notifications/toast';
import { statusAllowsApprove, statusAllowsDelete, statusAllowsPost, statusAllowsReject, statusAllowsReverse, statusAllowsSubmit } from '@/features/documents/document-labels';
import { useAuthStore } from '@/core/auth/auth.store';
import { PERMISSIONS } from '@/core/auth/permissions';
import type { DocumentDetailDto } from '@/core/accounting/documents.types';

export interface VoucherWorkflowProps {
  document: DocumentDetailDto;
  onChanged: () => void;
  onDelete?: () => void;
}

type DialogKind = 'submit' | 'approve' | 'reject' | 'post' | 'reverse' | 'delete' | null;

export const VoucherWorkflow: React.FC<VoucherWorkflowProps> = ({ document, onChanged, onDelete }) => {
  const queryClient = useQueryClient();
  const hasAny = useAuthStore((state) => state.hasAnyPermission);
  const canSubmit = hasAny([PERMISSIONS.documentsSubmit]);
  const canApprove = hasAny([PERMISSIONS.documentsApprove]);
  const canReject = hasAny([PERMISSIONS.documentsReject]);
  const canPost = hasAny([PERMISSIONS.documentsPost]);
  const canReverse = hasAny([PERMISSIONS.documentsReverse]);
  const canDelete = hasAny([PERMISSIONS.documentsDelete]);
  const [dialog, setDialog] = React.useState<DialogKind>(null);
  const [rejectionReason, setRejectionReason] = React.useState('');
  const [reversalDate, setReversalDate] = React.useState('');
  const [reversalDescription, setReversalDescription] = React.useState('');

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ['documents'] });
  };

  /* هر mutation: isPending → دکمه disable (بدون کلیک تکراری)، خطا با پیام فارسی backend */
  const submitMutation = useMutation({
    mutationFn: () => submitDocument(document.id),
    onSuccess: () => {
      toast.success('سند ارسال شد', 'وضعیت: در انتظار تأیید — جریان تأیید backend آغاز شد.');
      setDialog(null);
      invalidate();
      onChanged();
    },
    onError: (error) => { toast.error('ارسال سند انجام نشد', error instanceof ApiError ? error.message : undefined); setDialog(null); },
  });

  const approveMutation = useMutation({
    mutationFn: () => approveDocument(document.id),
    onSuccess: (result) => {
      toast.success('سند تأیید شد', result.status === 'Approved' ? 'آماده ثبت قطعی است.' : 'در مرحله بعدی جریان تأیید قرار گرفت.');
      setDialog(null);
      invalidate();
      onChanged();
    },
    onError: (error) => {
      if (error instanceof ApiError && error.code === 'Accounting.SodCreatorCannotApprove') {
        toast.error('تأیید انجام نشد', 'ایجادکننده سند نمی‌تواند همان سند را تأیید کند (جداسازی وظایف).');
      } else {
        toast.error('تأیید سند انجام نشد', error instanceof ApiError ? error.message : undefined);
      }
      setDialog(null);
    },
  });

  const rejectMutation = useMutation({
    mutationFn: () => rejectDocument(document.id, { rejectionReason: rejectionReason.trim() }),
    onSuccess: () => {
      toast.success('سند رد شد', 'سند به پیش‌نویس بازگشت و برای اصلاح قابل ویرایش است.');
      setDialog(null);
      setRejectionReason('');
      invalidate();
      onChanged();
    },
    onError: (error) => { toast.error('رد سند انجام نشد', error instanceof ApiError ? error.message : 'دلیل رد سند الزامی است.'); },
  });

  const postMutation = useMutation({
    mutationFn: () => postDocument(document.id),
    onSuccess: (result) => {
      toast.success('سند ثبت قطعی شد', `شماره نهایی ${result.documentNumber} — اصلاح فقط با سند معکوس/تعدیل.`);
      setDialog(null);
      invalidate();
      onChanged();
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 409) {
        toast.error('تعارض ثبت هم‌زمان', 'سند ممکن است توسط کاربر دیگری ثبت شده باشد؛ داده تازه‌سازی می‌شود.');
        void queryClient.invalidateQueries({ queryKey: ['documents'] });
      } else {
        toast.error('ثبت قطعی انجام نشد', error instanceof ApiError ? error.message : 'پیش‌نیازهای ثبت کامل نیست (مثلاً دوره بسته).');
      }
      setDialog(null);
    },
  });

  const reverseMutation = useMutation({
    mutationFn: () => reverseDocument(document.id, { reversalDate, description: reversalDescription || null, idempotencyKey: crypto.randomUUID() }),
    onSuccess: (result) => {
      toast.success('سند معکوس صادر شد', `شماره سند معکوس: ${result.reversalDocumentNumber}`);
      setDialog(null);
      invalidate();
      onChanged();
    },
    onError: (error) => { toast.error('صدور سند معکوس انجام نشد', error instanceof ApiError ? error.message : undefined); },
  });

  const deleteMutation = useMutation({
    mutationFn: () => deleteDocument(document.id),
    onSuccess: () => {
      toast.success('پیش‌نویس حذف شد', 'سند حذف‌شده دیگر در لیست اسناد نیست.');
      setDialog(null);
      invalidate();
      onDelete?.();
    },
    onError: (error) => { toast.error('حذف انجام نشد', error instanceof ApiError ? error.message : 'فقط پیش‌نویس قابل حذف است.'); setDialog(null); },
  });

  const anyPending =
    submitMutation.isPending || approveMutation.isPending || rejectMutation.isPending ||
    postMutation.isPending || reverseMutation.isPending || deleteMutation.isPending;

  const closeDialog = (open: boolean) => { if (!open) setDialog(null); };

  return (
    <div className="flex flex-wrap items-center gap-2" data-testid="voucher-workflow">
      {canSubmit && statusAllowsSubmit(document.status) && (
        <Button size="sm" onClick={() => setDialog('submit')} disabled={anyPending} data-testid="action-submit">
          <Send className="size-3.5" aria-hidden />ارسال برای تأیید
        </Button>
      )}
      {canApprove && statusAllowsApprove(document.status) && (
        <Button size="sm" onClick={() => setDialog('approve')} disabled={anyPending} data-testid="action-approve">
          <CheckCheck className="size-3.5" aria-hidden />تأیید
        </Button>
      )}
      {canReject && statusAllowsReject(document.status) && (
        <Button size="sm" variant="outline" className="text-danger" onClick={() => setDialog('reject')} disabled={anyPending} data-testid="action-reject">
          <Ban className="size-3.5" aria-hidden />رد
        </Button>
      )}
      {canPost && statusAllowsPost(document.status) && (
        <Button size="sm" onClick={() => setDialog('post')} disabled={anyPending} data-testid="action-post">
          <FileSignature className="size-3.5" aria-hidden />ثبت قطعی
        </Button>
      )}
      {canReverse && statusAllowsReverse(document.status) && (
        <Button size="sm" variant="outline" className="text-warning" onClick={() => setDialog('reverse')} disabled={anyPending} data-testid="action-reverse">
          <Undo2 className="size-3.5" aria-hidden />سند معکوس
        </Button>
      )}
      {canDelete && statusAllowsDelete(document.status) && (
        <Button size="sm" variant="outline" className="text-danger" onClick={() => setDialog('delete')} disabled={anyPending} data-testid="action-delete">
          <Trash2 className="size-3.5" aria-hidden />حذف پیش‌نویس
        </Button>
      )}

      {/* تأییدها */}
      <ConfirmDialog open={dialog === 'submit'} onOpenChange={closeDialog} title="ارسال سند برای تأیید"
        description="سند از پیش‌نویس خارج و در جریان تأیید backend قرار می‌گیرد؛ دیگر قابل ویرایش نیست."
        actionLabel="ارسال" onConfirm={() => submitMutation.mutate()} />
      <ConfirmDialog open={dialog === 'approve'} onOpenChange={closeDialog} title="تأیید سند"
        description="طبق سیاست تأیید backend، سند به مرحله بعد می‌رود؛ ایجادکننده نمی‌تواند سند خودش را تأیید کند (SOD)."
        actionLabel="تأیید سند" onConfirm={() => approveMutation.mutate()} />
      <ConfirmDialog open={dialog === 'post'} onOpenChange={closeDialog} title="ثبت قطعی سند"
        description={
          <span>عملیات برگشت‌ناپذیر است: مانده‌ها دائم می‌شوند و اصلاح فقط با سند معکوس یا تعدیل ممکن است.</span>
        }
        actionLabel="ثبت قطعی" onConfirm={() => postMutation.mutate()} />
      <ConfirmDialog open={dialog === 'delete'} onOpenChange={closeDialog} title="حذف پیش‌نویس"
        description="پیش‌نویس به‌صورت فیزیکی حذف می‌شود و قابل بازیابی نیست."
        actionLabel="حذف قطعی" onConfirm={() => deleteMutation.mutate()} />

      {/* رد با دلیل الزامی */}
      <Modal open={dialog === 'reject'} onOpenChange={closeDialog} title="رد سند" size="sm">
        <div className="space-y-3">
          <p className="text-xs text-muted-foreground">سند به پیش‌نویس باز می‌گردد تا اصلاح شود. دلیل رد در تاریخچه ثبت می‌شود.</p>
          <label htmlFor="rejection-reason" className="block text-xs font-semibold">دلیل رد *</label>
          <Textarea id="rejection-reason" rows={3} value={rejectionReason} onChange={(event) => setRejectionReason(event.target.value)} maxLength={500} data-testid="rejection-reason" />
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => setDialog(null)}>انصراف</Button>
            <Button className="text-white" disabled={!rejectionReason.trim() || rejectMutation.isPending} onClick={() => rejectMutation.mutate()} data-testid="confirm-reject">
              {rejectMutation.isPending ? 'در حال ثبت…' : 'ثبت رد'}
            </Button>
          </div>
        </div>
      </Modal>

      {/* معکوس با تاریخ */}
      <Modal open={dialog === 'reverse'} onOpenChange={closeDialog} title="صدور سند معکوس" size="sm">
        <div className="space-y-3">
          <p className="text-xs text-muted-foreground">سندی با اثر عکس سند اصلی صادر می‌شود؛ سند اصلی با وضعیت «باطل‌شده» علامت می‌خورد.</p>
          <label htmlFor="reversal-date" className="block text-xs font-semibold">تاریخ سند معکوس *</label>
          <Input id="reversal-date" type="date" dir="ltr" value={reversalDate} onChange={(event) => setReversalDate(event.target.value)} data-testid="reversal-date" />
          <label htmlFor="reversal-desc" className="block text-xs font-semibold">شرح (اختیاری)</label>
          <Textarea id="reversal-desc" rows={2} value={reversalDescription} onChange={(event) => setReversalDescription(event.target.value)} placeholder="مثلاً اصلاح ثبت اشتباه" data-testid="reversal-description" />
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => setDialog(null)}>انصراف</Button>
            <Button disabled={!reversalDate || reverseMutation.isPending} onClick={() => reverseMutation.mutate()} data-testid="confirm-reverse">
              {reverseMutation.isPending ? 'در حال صدور…' : 'صدور سند معکوس'}
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
};
