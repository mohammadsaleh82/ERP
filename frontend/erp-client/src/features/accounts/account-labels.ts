/** برچسب‌های فارسی — دقیقاً مطابق enumهای واقعی backend (AccountingEnums.cs) */
import type { AccountLevel, AccountNature, NormalBalance } from '@/core/accounting/accounts.types';

export const accountLevelLabels: Record<string, string> = {
  Group: 'گروه',
  General: 'کل',
  Subsidiary: 'معین',
  Detail: 'تفصیلی',
};

export const accountNatureLabels: Record<string, string> = {
  Asset: 'دارایی',
  Liability: 'بدهی',
  Equity: 'سرمایه',
  Revenue: 'درآمد',
  Expense: 'هزینه',
};

export const normalBalanceLabels: Record<string, string> = {
  Debit: 'بدهکار',
  Credit: 'بستانکار',
};

/** سطح برچسب از مقدار API — سطح ناشناخته به‌جای شکست، همان مقدار خام را نشان می‌دهد */
export const levelLabel = (level: AccountLevel): string => accountLevelLabels[level] ?? String(level);
export const natureLabel = (nature: AccountNature): string => accountNatureLabels[nature] ?? String(nature);
export const normalBalanceLabel = (balance: NormalBalance): string => normalBalanceLabels[balance] ?? String(balance);
