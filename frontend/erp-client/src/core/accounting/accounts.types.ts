/**
 * انواع حساب‌ها — منبع واقعی: src/ERP.Modules.Accounting/Application/DTOs/AccountDtos.cs
 * سطح‌بندی مطابق Domain (Group/General/Subsidiary/Detail) و درخت عمق‌متغیر از API.
 */

export type AccountLevel = 'Group' | 'General' | 'Subsidiary' | 'Detail' | string;
export type AccountNature = 'Asset' | 'Liability' | 'Equity' | 'Revenue' | 'Expense' | string;
export type NormalBalance = 'Debit' | 'Credit' | string;

export interface AccountDto {
  id: string;
  tenantId: string;
  companyId: string;
  parentAccountId: string | null;
  code: string;
  fullCode: string;
  name: string;
  description: string | null;
  level: AccountLevel;
  nature: AccountNature;
  normalBalance: NormalBalance;
  isActive: boolean;
  isSystemAccount: boolean;
  allowPosting: boolean;
  hasChildren: boolean;
  requireCostCenter: boolean;
  requireProject: boolean;
  requireDetailAccount: boolean;
  createdAtUtc: string;
  lastModifiedAtUtc: string | null;
}

export interface AccountTreeNodeDto {
  id: string;
  parentAccountId: string | null;
  code: string;
  fullCode: string;
  name: string;
  level: AccountLevel;
  nature: AccountNature;
  normalBalance: NormalBalance;
  isActive: boolean;
  allowPosting: boolean;
  children: AccountTreeNodeDto[];
}

export interface AccountBreadcrumbDto {
  id: string;
  code: string;
  fullCode: string;
  name: string;
  level: AccountLevel;
}

export interface CreateAccountRequest {
  parentAccountId: string | null;
  code: string;
  name: string;
  description: string | null;
  nature: AccountNature | null;
  normalBalance: NormalBalance | null;
  allowPosting: boolean;
  requireCostCenter: boolean;
  requireProject: boolean;
  requireDetailAccount: boolean;
}

/** قید Domain: پس از ایجاد فقط این فیلدها قابل ویرایش‌اند (UpdateAccountRequest) */
export interface UpdateAccountRequest {
  name: string;
  description: string | null;
  normalBalance: NormalBalance;
  requireCostCenter: boolean;
  requireProject: boolean;
  requireDetailAccount: boolean;
}

export interface AccountFilterRequest {
  searchTerm?: string;
  level?: AccountLevel;
  nature?: AccountNature;
  isActive?: boolean;
  allowPosting?: boolean;
  parentAccountId?: string;
  page?: number;
  pageSize?: number;
}

export interface PagedAccountsResult {
  items: AccountDto[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}
