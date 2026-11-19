import { describe, expect, it } from 'vitest';
import {
  fiscalPeriodStatusLabel,
  voucherStatusLabel,
  VOUCHER_STATUS_MAP,
  FISCAL_PERIOD_STATUS_MAP,
} from '@/shared/utils/status';

describe('financial status mapping', () => {
  it('maps every voucher status to a Persian label and stable tone', () => {
    expect(VOUCHER_STATUS_MAP.draft).toEqual({ label: 'پیش‌نویس', tone: 'neutral' });
    expect(VOUCHER_STATUS_MAP.posted).toEqual({ label: 'قطعی شده', tone: 'success' });
    expect(VOUCHER_STATUS_MAP.pending).toEqual({ label: 'در انتظار تأیید', tone: 'warning' });
    expect(VOUCHER_STATUS_MAP.reversed).toEqual({ label: 'ابطال/برگشتی', tone: 'info' });
  });

  it('maps fiscal period statuses', () => {
    expect(FISCAL_PERIOD_STATUS_MAP.open.label).toBe('باز');
    expect(FISCAL_PERIOD_STATUS_MAP.closed.label).toBe('بسته');
    expect(FISCAL_PERIOD_STATUS_MAP.adjusting.label).toBe('در حال تعدیل');
  });

  it('falls back gracefully for unknown backend statuses', () => {
    expect(voucherStatusLabel('exotic-status')).toEqual({ label: 'exotic-status', tone: 'neutral' });
    expect(fiscalPeriodStatusLabel('weird').tone).toBe('neutral');
  });
});
