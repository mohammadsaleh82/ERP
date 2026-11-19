/**
 * انواع اسناد حسابداری — منبع واقعی: DocumentDtos.cs + AccountingEnums.cs
 * مبالغ در این لایه رشته decimal (تمام‌دقیق با decimal.js) و در ارسال به API
 * با toApiNumber تبدیل به number می‌شوند (سرور decimal را از number می‌بندد).
 */
export type DocumentStatus = 'Draft' | 'PendingApproval' | 'Reviewed' | 'Approved' | 'Posted' | 'Reversed' | string;
export type DocumentType = 'General' | 'Opening' | 'Closing' | 'Adjustment' | 'Reversal' | string;
export type DebitCreditSide = 'Debit' | 'Credit';

export interface VoucherLineInput {
  accountId: string;
  side: DebitCreditSide;
  /** مبلغ decimal-safe — رشته تمام‌دقیق (بدون float) */
  amount: string;
  description: string;
  costCenterId?: string | null;
  projectId?: string | null;
  detailAccountId?: string | null;
  foreignCurrencyCode?: string | null;
  foreignAmount?: string | null;
  exchangeRate?: number;
}

export interface CreateDocumentRequest {
  documentDate: string;
  description: string;
  lines: VoucherLineInput[];
  fiscalYearId?: string | null;
  documentType?: DocumentType;
  referenceNumber?: string | null;
  idempotencyKey?: string | null;
}

export interface UpdateDocumentRequest {
  documentDate: string;
  description: string;
  lines: VoucherLineInput[];
  referenceNumber?: string | null;
}

export interface SearchDocumentsRequest {
  status?: DocumentStatus;
  fiscalYearId?: string;
  fromDate?: string;
  toDate?: string;
  sourceModule?: string;
  documentNumber?: number;
  pageNumber?: number;
  pageSize?: number;
  sortBy?: string;
}

export interface RejectDocumentRequest {
  rejectionReason: string;
}

export interface ReverseDocumentRequest {
  reversalDate: string;
  description?: string | null;
  idempotencyKey?: string | null;
}

export interface DocumentLineDto {
  id: string;
  accountId: string;
  accountFullCode: string;
  accountName: string;
  lineNumber: number;
  side: DebitCreditSide;
  /** decimal-safe از سرور به رشته تبدیل می‌شود */
  debitAmount: string;
  creditAmount: string;
  description: string;
  costCenterId: string | null;
  projectId: string | null;
  detailAccountId: string | null;
  foreignCurrencyCode: string | null;
  foreignCurrencyAmount: string | null;
  exchangeRate: number;
}

export interface DocumentDto {
  id: string;
  tenantId: string;
  companyId: string;
  fiscalYearId: string;
  fiscalPeriodId: string;
  documentNumber: number;
  documentDate: string;
  description: string;
  documentType: DocumentType;
  status: DocumentStatus;
  referenceNumber: string | null;
  sourceModule: string | null;
  sourceDocumentId: string | null;
  reversedDocumentId: string | null;
  totalDebit: string;
  totalCredit: string;
  currencyCode: string;
  rejectionReason: string | null;
  createdBy: string | null;
  createdAtUtc: string;
  submittedBy: string | null;
  submittedAtUtc: string | null;
  approvedBy: string | null;
  approvedAtUtc: string | null;
  postedBy: string | null;
  postedAtUtc: string | null;
  linesCount: number;
}

export interface DocumentDetailDto extends DocumentDto {
  isBalanced: boolean;
  lines: DocumentLineDto[];
}

export interface PostDocumentResultDto {
  documentId: string;
  documentNumber: number;
  totalDebit: string;
  totalCredit: string;
  postedAtUtc: string;
  postedBy: string;
  wasAlreadyPosted: boolean;
}

export interface ReverseDocumentResultDto {
  originalDocumentId: string;
  reversalDocumentId: string;
  reversalDocumentNumber: number;
  totalAmount: string;
  wasAlreadyReversed: boolean;
}

export interface DocumentAuditLogDto {
  id: string;
  documentId: string;
  action: string;
  previousStatus: DocumentStatus | null;
  newStatus: DocumentStatus | null;
  details: string | null;
  performedBy: string;
  performedAtUtc: string;
}
