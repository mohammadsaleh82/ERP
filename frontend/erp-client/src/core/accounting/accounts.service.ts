/** سرویس حساب‌ها — endpointهای واقعی: /api/v1/accounting/accounts */
import { apiClient } from '@/core/api/client';
import type {
  AccountBreadcrumbDto,
  AccountDto,
  AccountFilterRequest,
  AccountTreeNodeDto,
  CreateAccountRequest,
  PagedAccountsResult,
  UpdateAccountRequest,
} from './accounts.types';

const BASE = '/Accounts';

export async function getAccountTree(): Promise<AccountTreeNodeDto[]> {
  const { data } = await apiClient.get<AccountTreeNodeDto[]>(`${BASE}/tree`);
  return data ?? [];
}

export async function getAccountChildren(parentId: string): Promise<AccountDto[]> {
  const { data } = await apiClient.get<AccountDto[]>(`${BASE}/${parentId}/children`);
  return data ?? [];
}

export async function getAccountBreadcrumb(id: string): Promise<AccountBreadcrumbDto[]> {
  const { data } = await apiClient.get<AccountBreadcrumbDto[]>(`${BASE}/${id}/breadcrumb`);
  return data ?? [];
}

export async function getPostableAccounts(): Promise<AccountDto[]> {
  const { data } = await apiClient.get<AccountDto[]>(`${BASE}/postable`);
  return data ?? [];
}

export async function searchAccounts(filter: AccountFilterRequest): Promise<PagedAccountsResult> {
  const { data } = await apiClient.get<PagedAccountsResult>(BASE, { params: filter });
  return data ?? { items: [], totalCount: 0, pageNumber: filter.page ?? 1, pageSize: filter.pageSize ?? 50 };
}

export async function getAccount(id: string): Promise<AccountDto> {
  const { data } = await apiClient.get<AccountDto>(`${BASE}/${id}`);
  return data;
}

export async function getAccountByCode(code: string): Promise<AccountDto> {
  const { data } = await apiClient.get<AccountDto>(`${BASE}/by-code/${code}`);
  return data;
}

export async function createAccount(request: CreateAccountRequest): Promise<AccountDto> {
  const { data } = await apiClient.post<AccountDto>(BASE, request);
  return data;
}

export async function updateAccount(id: string, request: UpdateAccountRequest): Promise<AccountDto> {
  const { data } = await apiClient.put<AccountDto>(`${BASE}/${id}`, request);
  return data;
}

export async function deactivateAccount(id: string): Promise<AccountDto> {
  const { data } = await apiClient.post<AccountDto>(`${BASE}/${id}/deactivate`);
  return data;
}

export async function activateAccount(id: string): Promise<AccountDto> {
  const { data } = await apiClient.post<AccountDto>(`${BASE}/${id}/activate`);
  return data;
}

export async function deleteAccount(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/${id}`);
}
