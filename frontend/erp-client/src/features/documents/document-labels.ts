/** برچسب‌های اسناد — مطابق DocumentStatus/DocumentType واقعی backend */
import type { DocumentStatus, DocumentType } from '@/core/accounting/documents.types';

export const documentStatusLabels: Record<string, string> = {
  Draft: 'پیش‌نویس',
  PendingApproval: 'در انتظار تأیید',
  Reviewed: 'بررسی‌شده',
  Approved: 'تاییدشده',
  Posted: 'ثبت قطعی',
  Reversed: 'باطل‌شده',
};

export const documentTypeLabels: Record<string, string> = {
  General: 'عادی',
  Opening: 'افتتاحیه',
  Closing: 'اختتامیه',
  Adjustment: 'تعدیلات',
  Reversal: 'معکوس',
};

export const statusLabel = (status: DocumentStatus): string => documentStatusLabels[status] ?? String(status);
export const typeLabel = (type: DocumentType): string => documentTypeLabels[type] ?? String(type);

/** variant بج وضعیت */
export const statusVariant = (status: DocumentStatus): 'secondary' | 'success' | 'warning' | 'destructive' | 'outline' => {
  switch (status) {
    case 'Posted': return 'success';
    case 'Draft': return 'secondary';
    case 'PendingApproval': return 'warning';
    case 'Reviewed': return 'warning';
    case 'Approved': return 'outline';
    case 'Reversed': return 'destructive';
    default: return 'secondary';
  }
};

/** عملیات مجاز هر وضعیت — مطابق قانون backend (DocumentNotEditable) */
export const statusAllowsEdit = (status: DocumentStatus): boolean => status === 'Draft';
export const statusAllowsDelete = (status: DocumentStatus): boolean => status === 'Draft';
export const statusAllowsSubmit = (status: DocumentStatus): boolean => status === 'Draft';
export const statusAllowsApprove = (status: DocumentStatus): boolean => status === 'PendingApproval' || status === 'Reviewed';
export const statusAllowsReject = (status: DocumentStatus): boolean => status === 'PendingApproval' || status === 'Reviewed';
export const statusAllowsPost = (status: DocumentStatus): boolean => status === 'Approved';
export const statusAllowsReverse = (status: DocumentStatus): boolean => status === 'Posted';
