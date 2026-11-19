/** سرویس تنظیمات حسابداری — endpointهای واقعی: /api/v1/accounting/settings */
import { apiClient } from '@/core/api/client';
import type {
  AccountingSettingsDto,
  AccountingSettingsHistoryDto,
  UpdateAccountingSettingsRequest,
} from './settings.types';

const BASE = '/accounting/settings';

export async function getAccountingSettings(): Promise<AccountingSettingsDto> {
  const { data } = await apiClient.get<AccountingSettingsDto>(BASE);
  return data;
}

/** ذخیره با نسخه (optimistic concurrency) — conflict 409 را بالا پرتاب می‌کند */
export async function updateAccountingSettings(request: UpdateAccountingSettingsRequest): Promise<AccountingSettingsDto> {
  const { data } = await apiClient.put<AccountingSettingsDto>(BASE, request);
  return data;
}

export async function getSettingsHistory(): Promise<AccountingSettingsHistoryDto[]> {
  const { data } = await apiClient.get<AccountingSettingsHistoryDto[]>(`${BASE}/history`);
  return data ?? [];
}
