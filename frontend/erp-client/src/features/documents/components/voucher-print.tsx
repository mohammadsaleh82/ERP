/**
 * قالب چاپ سند — A4، راست‌چین، بدون اجزای UI (hidden تا print + print:block).
 * اعداد تبلار با فرمت هزارگان، جمع‌ها decimal-safe از سرور (totalDebit/totalCredit).
 */
import * as React from 'react';
import { formatAmount } from '@/core/accounting/documents.service';
import { statusLabel, typeLabel } from '@/features/documents/document-labels';
import { formatToPersianDate } from '@/shared/utils/date';
import { digitsToPersian } from '@/shared/utils/number';
import type { DocumentDetailDto } from '@/core/accounting/documents.types';

export const VoucherPrint: React.FC<{ document: DocumentDetailDto; companyName: string }> = ({ document, companyName }) => (
  <div className="hidden print:block" aria-hidden>
    {/* در چاپ نمایش داده می‌شود؛ در صفحه مخفی */}
    <div className="voucher-print" dir="rtl" style={{ fontFamily: 'Vazirmatn, Tahoma, sans-serif', fontSize: '12px' }} data-testid="voucher-print">
      <div style={{ textAlign: 'center', borderBottom: '2px solid #111', paddingBottom: '8px', marginBottom: '10px' }}>
        <div style={{ fontSize: '16px', fontWeight: 900 }}>{companyName}</div>
        <div style={{ fontSize: '13px', fontWeight: 700, marginTop: '4px' }}>سند حسابداری شماره {digitsToPersian(document.documentNumber)}</div>
      </div>

      <table style={{ width: '100%', fontSize: '11px', marginBottom: '10px', borderCollapse: 'collapse' }}>
        <tbody>
          <tr>
            <td style={{ padding: '3px 6px' }}>تاریخ: <b>{formatToPersianDate(document.documentDate)}</b></td>
            <td style={{ padding: '3px 6px' }}>نوع: <b>{typeLabel(document.documentType)}</b></td>
            <td style={{ padding: '3px 6px' }}>وضعیت: <b>{statusLabel(document.status)}</b></td>
            <td style={{ padding: '3px 6px' }}>ارز: <b>{document.currencyCode}</b></td>
          </tr>
          <tr>
            <td style={{ padding: '3px 6px' }} colSpan={2}>مرجع: <b>{document.referenceNumber ?? '—'}</b></td>
            <td style={{ padding: '3px 6px' }} colSpan={2}>ایجادکننده: <b>{document.createdBy ?? '—'}</b></td>
          </tr>
          <tr>
            <td style={{ padding: '3px 6px' }} colSpan={4}>شرح سند: <b>{document.description}</b></td>
          </tr>
        </tbody>
      </table>

      <table style={{ width: '100%', fontSize: '11px', borderCollapse: 'collapse', textAlign: 'right' }}>
        <thead>
          <tr style={{ background: '#f1f1f1', borderBottom: '1px solid #111' }}>
            <th style={{ border: '1px solid #999', padding: '4px' }}>ردیف</th>
            <th style={{ border: '1px solid #999', padding: '4px' }}>کد حساب</th>
            <th style={{ border: '1px solid #999', padding: '4px' }}>نام حساب</th>
            <th style={{ border: '1px solid #999', padding: '4px' }}>شرح</th>
            <th style={{ border: '1px solid #999', padding: '4px' }}>بدهکار</th>
            <th style={{ border: '1px solid #999', padding: '4px' }}>بستانکار</th>
          </tr>
        </thead>
        <tbody>
          {document.lines.map((line) => (
            <tr key={line.id}>
              <td style={{ border: '1px solid #999', padding: '4px', textAlign: 'center' }}>{digitsToPersian(line.lineNumber)}</td>
              <td style={{ border: '1px solid #999', padding: '4px', direction: 'ltr' }}>{line.accountFullCode}</td>
              <td style={{ border: '1px solid #999', padding: '4px' }}>{line.accountName}</td>
              <td style={{ border: '1px solid #999', padding: '4px' }}>{line.description}</td>
              <td style={{ border: '1px solid #999', padding: '4px', textAlign: 'left', fontVariantNumeric: 'tabular-nums' }}>
                {Number(line.debitAmount) !== 0 ? formatAmount(line.debitAmount) : '—'}
              </td>
              <td style={{ border: '1px solid #999', padding: '4px', textAlign: 'left', fontVariantNumeric: 'tabular-nums' }}>
                {Number(line.creditAmount) !== 0 ? formatAmount(line.creditAmount) : '—'}
              </td>
            </tr>
          ))}
          <tr style={{ fontWeight: 900, background: '#f8f8f8' }}>
            <td style={{ border: '1px solid #999', padding: '4px' }} colSpan={4}>جمع کل</td>
            <td style={{ border: '1px solid #999', padding: '4px', textAlign: 'left', fontVariantNumeric: 'tabular-nums' }}>{formatAmount(document.totalDebit)}</td>
            <td style={{ border: '1px solid #999', padding: '4px', textAlign: 'left', fontVariantNumeric: 'tabular-nums' }}>{formatAmount(document.totalCredit)}</td>
          </tr>
        </tbody>
      </table>

      <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: '28px', fontSize: '11px' }}>
        <span>تهیه‌کننده: {document.createdBy ?? '—'}</span>
        <span>تأییدکننده: {document.approvedBy ?? '—'}</span>
        <span>ثبت‌کننده: {document.postedBy ?? '—'}</span>
      </div>
      <div style={{ marginTop: '10px', fontSize: '9px', color: '#555' }}>
        این سند به‌صورت سیستمی تولید شده است — امضای الکترونیکی backend: {document.id}
      </div>
    </div>
  </div>
);
