/** سرویس اسناد — endpointهای واقعی: /api/v1/accounting/documents */
import { apiClient } from '@/core/api/client';
import { Decimal } from 'decimal.js';
import type {
  CreateDocumentRequest,
  DebitCreditSide,
  DocumentAuditLogDto,
  DocumentDetailDto,
  DocumentDto,
  DocumentLineDto,
  PostDocumentResultDto,
  RejectDocumentRequest,
  ReverseDocumentRequest,
  ReverseDocumentResultDto,
  SearchDocumentsRequest,
  UpdateDocumentRequest,
  VoucherLineInput,
} from './documents.types';

const BASE = '/Documents';

/** decimal.js با دقت کافی برای مبالغ تا ۱۵ رقم صحیح + ۷ اعشار */
Decimal.set({ precision: 40, rounding: Decimal.ROUND_HALF_UP });

/* ── Decimal-safe helpers ─────────────────────────────────────────── */

/** تبدیل ورودی کاربر (ارقام فارسی/عربی، جداکننده هزارگان) به رشته decimal تمیز — بدون float */
export function parseAmountInput(raw: string): string {
  const persian = '۰۱۲۳۴۵۶۷۸۹';
  const arabic = '٠١٢٣٤٥٦٧٨٩';
  let text = String(raw ?? '')
    .replace(/[۰-۹]/g, (d) => String(persian.indexOf(d)))
    .replace(/[٠-٩]/g, (d) => String(arabic.indexOf(d)))
    .replace(/[٬,،\s]/g, '') // جداکننده هزارگان فارسی/لاتین
    .replace(/[٫]/g, '.'); // ممیز فارسی
  if (text === '') return '';
  if (!/^-?\d*(\.\d*)?$/.test(text)) return 'NaN';
  text = text.replace(/\.+$/, '');
  try {
    const value = new Decimal(text === '-' ? '0' : text || '0');
    if (!value.isFinite()) return 'NaN';
    return value.toFixed();
  } catch {
    return 'NaN';
  }
}

/** قالب نمایش: جداکننده هزارگان با ارقام انگلیسی (خوانایی جدول) */
export function formatAmount(value: string | number | null | undefined): string {
  if (value === null || value === undefined || value === '') return '—';
  const decimal = new Decimal(String(value));
  if (decimal.isZero()) return '—';
  return decimal.toDecimalPlaces(7).toString().replace(/\B(?=(\d{3})+(?!\d))/g, ',');
}

/** جمع تمام‌دقیق چند مبلغ رشته‌ای */
export function sumAmounts(values: string[]): string {
  return values.reduce((acc, value) => acc.plus(new Decimal(value || '0')), new Decimal(0)).toFixed();
}

/** ارسال به API: سرور decimal را از number می‌بندد؛ guard اطمینان از round-trip بدون از دست رفتن دقت */
export function toApiNumber(value: string): number {
  const decimal = new Decimal(value || '0');
  const asNumber = Number(decimal.toString());
  if (!Number.isFinite(asNumber)) throw new Error('مبلغ خارج از محدوده قابل ارسال است');
  if (new Decimal(asNumber.toString()).minus(decimal).abs().greaterThan('1e-12')) {
    throw new Error(`دقت مبلغ ${decimal.toString()} در ارسال حفظ نمی‌شود — مقدار را کوتاه‌تر کنید`);
  }
  return asNumber;
}

/** تبدیل ردیف فرم به VoucherLineInput واقعی سرور (amount → number) */
export function toApiLine(line: VoucherLineInput): Record<string, unknown> {
  return {
    accountId: line.accountId,
    side: line.side,
    amount: toApiNumber(line.amount),
    description: line.description,
    costCenterId: line.costCenterId ?? null,
    projectId: line.projectId ?? null,
    detailAccountId: line.detailAccountId ?? null,
    foreignCurrencyCode: line.foreignCurrencyCode ?? null,
    foreignAmount: line.foreignAmount ? toApiNumber(line.foreignAmount) : null,
    exchangeRate: line.exchangeRate ?? 1.0,
  };
}

/* ── CRUD + گردش کار ─────────────────────────────────────────────── */

export async function searchDocuments(filter: SearchDocumentsRequest): Promise<PagedDocuments> {
  const { data } = await apiClient.get<PagedDocuments>(BASE, { params: filter });
  return data ?? { items: [], totalCount: 0, pageNumber: 1, pageSize: 20 };
}

export interface PagedDocuments {
  items: DocumentDto[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export async function getDocument(id: string): Promise<DocumentDetailDto> {
  const { data } = await apiClient.get<DocumentDetailDto>(`${BASE}/${id}`);
  return data;
}

export async function getDocumentAuditTrail(id: string): Promise<DocumentAuditLogDto[]> {
  const { data } = await apiClient.get<DocumentAuditLogDto[]>(`${BASE}/${id}/audit-trail`);
  return data ?? [];
}

export async function createDocument(request: CreateDocumentRequest): Promise<DocumentDto> {
  const { data } = await apiClient.post<DocumentDto>(BASE, {
    ...request,
    lines: request.lines.map(toApiLine),
  });
  return data;
}

export async function updateDocument(id: string, request: UpdateDocumentRequest): Promise<DocumentDto> {
  const { data } = await apiClient.put<DocumentDto>(`${BASE}/${id}`, {
    ...request,
    lines: request.lines.map(toApiLine),
  });
  return data;
}

export async function deleteDocument(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/${id}`);
}

export async function submitDocument(id: string): Promise<DocumentDto> {
  const { data } = await apiClient.post<DocumentDto>(`${BASE}/${id}/submit`);
  return data;
}

export async function approveDocument(id: string): Promise<DocumentDto> {
  const { data } = await apiClient.post<DocumentDto>(`${BASE}/${id}/approve`);
  return data;
}

export async function rejectDocument(id: string, request: RejectDocumentRequest): Promise<DocumentDto> {
  const { data } = await apiClient.post<DocumentDto>(`${BASE}/${id}/reject`, request);
  return data;
}

export async function postDocument(id: string): Promise<PostDocumentResultDto> {
  const { data } = await apiClient.post<PostDocumentResultDto>(`${BASE}/${id}/post`);
  return data;
}

export async function reverseDocument(id: string, request: ReverseDocumentRequest): Promise<ReverseDocumentResultDto> {
  const { data } = await apiClient.post<ReverseDocumentResultDto>(`${BASE}/${id}/reverse`, request);
  return data;
}

/* ── اعتبارسنجی فرم — مکمل backend برای بازخورد سریع ─────────────── */

export interface LineDraft {
  key: string;
  accountId: string | null;
  accountFullCode: string;
  accountName: string;
  side: DebitCreditSide;
  /** ورودی خام کاربر برای ویرایش روان */
  amountRaw: string;
  /** مقدار decimal تمیز — از amountRaw با parseAmountInput */
  amount: string;
  description: string;
  costCenterId?: string | null;
  projectId?: string | null;
  detailAccountId?: string | null;
  foreignCurrencyCode?: string | null;
  foreignAmountRaw?: string;
  requireCostCenter: boolean;
  requireProject: boolean;
  requireDetailAccount: boolean;
}

/** نتیجه اعتبارسنجی سمت client — هر خطا با نوع مشخص تا UI پیام درست نشان دهد */
export interface FormValidation {
  isValid: boolean;
  errors: Array<{ type: 'description' | 'date' | 'unbalanced' | 'empty-line' | 'amount' | 'account' | 'detail'; message: string }>;
}

export function validateVoucherForm(description: string, documentDate: string, lines: LineDraft[], documentStatusCheck?: 'closed-period' | null): FormValidation {
  const errors: FormValidation['errors'] = [];
  if (!description.trim()) errors.push({ type: 'description', message: 'شرح سند حسابداری الزامی است.' });
  else if (description.length > 500) errors.push({ type: 'description', message: 'شرح سند حداکثر می‌تواند ۵۰۰ کاراکتر باشد.' });
  if (!documentDate) errors.push({ type: 'date', message: 'تاریخ سند الزامی است.' });

  const filled = lines.filter((line) => line.accountId || line.amount || line.description);
  if (filled.length === 0) errors.push({ type: 'empty-line', message: 'سند بدون ردیف قابل ثبت نیست — حداقل یک ردیف کامل کنید.' });

  for (const line of filled) {
    if (!line.accountId) errors.push({ type: 'account', message: 'شناسه حساب برای هر ردیف سند الزامی است.' });
    const amount = new Decimal(line.amount && line.amount !== 'NaN' ? line.amount : '0');
    if (amount.lessThanOrEqualTo(0)) errors.push({ type: 'amount', message: 'مبلغ ردیف سند باید عددی مثبت و بزرگتر از صفر باشد.' });
    else if (amount.greaterThan('999999999999999')) errors.push({ type: 'amount', message: 'مبلغ ردیف سند از سقف مجاز عبور کرده است.' });
    if (!line.description.trim()) errors.push({ type: 'empty-line', message: 'شرح ردیف سند الزامی است.' });
    else if (line.description.length > 500) errors.push({ type: 'empty-line', message: 'شرح ردیف سند حداکثر ۵۰۰ کاراکتر است.' });
    if (line.requireCostCenter && !line.costCenterId) errors.push({ type: 'detail', message: 'برای این حساب، مرکز هزینه الزامی است (DimensionRequired).' });
    if (line.requireProject && !line.projectId) errors.push({ type: 'detail', message: 'برای این حساب، پروژه الزامی است (DimensionRequired).' });
    if (line.requireDetailAccount && !line.detailAccountId) errors.push({ type: 'detail', message: 'برای این حساب، حساب تفصیلی الزامی است (DimensionRequired).' });
  }

  const totalDebit = sumAmounts(filled.filter((line) => line.side === 'Debit').map((line) => line.amount || '0'));
  const totalCredit = sumAmounts(filled.filter((line) => line.side === 'Credit').map((line) => line.amount || '0'));
  if (filled.length > 0 && !new Decimal(totalDebit).equals(totalCredit)) {
    errors.push({ type: 'unbalanced', message: 'سند نامتوازن است — جمع بدهکار و بستانکار باید برابر شود (backend سند نامتوازن نمی‌پذیرد).' });
  }

  if (documentStatusCheck === 'closed-period') {
    errors.push({ type: 'date', message: 'تاریخ سند در دوره بسته/قفل است — دوره مبدأ سند باز نیست (DocumentDateOutsideOpenPeriod).' });
  }

  return { isValid: errors.length === 0, errors };
}

/** جمع‌های جدول — همه با Decimal */
export function computeTotals(lines: LineDraft[]): { totalDebit: string; totalCredit: string; difference: string; balanced: boolean } {
  const totalDebit = sumAmounts(lines.filter((line) => line.side === 'Debit').map((line) => line.amount || '0'));
  const totalCredit = sumAmounts(lines.filter((line) => line.side === 'Credit').map((line) => line.amount || '0'));
  const difference = new Decimal(totalDebit).minus(totalCredit).toFixed();
  return { totalDebit, totalCredit, difference, balanced: new Decimal(difference).isZero() };
}

/** تبدیل DocumentDetailDto به LineDraft[] برای فرم ویرایش پیش‌نویس */
export function detailToLineDrafts(detail: DocumentDetailDto): LineDraft[] {
  return detail.lines.map((line: DocumentLineDto) => ({
    key: line.id,
    accountId: line.accountId,
    accountFullCode: line.accountFullCode,
    accountName: line.accountName,
    side: line.side,
    amountRaw: formatAmount(line.side === 'Debit' ? line.debitAmount : line.creditAmount).replace(/,/g, ''),
    amount: line.side === 'Debit' ? String(line.debitAmount) : String(line.creditAmount),
    description: line.description,
    costCenterId: line.costCenterId,
    projectId: line.projectId,
    detailAccountId: line.detailAccountId,
    foreignCurrencyCode: line.foreignCurrencyCode,
    foreignAmountRaw: line.foreignCurrencyAmount ? String(line.foreignCurrencyAmount) : undefined,
    requireCostCenter: false,
    requireProject: false,
    requireDetailAccount: false,
  }));
}
