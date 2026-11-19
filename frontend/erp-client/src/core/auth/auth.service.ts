/**
 * سرویس احراز هویت — قرارداد واقعی: src/ERP.Api/Controllers/AuthController.cs
 * POST /Auth/login · POST /Auth/refresh-token · POST /Auth/logout · GET /Auth/me · GET /Auth/companies
 * Refresh Token فقط در Cookie HttpOnly می‌ماند؛ Access Token فقط در حافظه (Zustand).
 */
import axios from 'axios';
import { apiClient } from '@/core/api/client';
import { ApiError } from '@/core/errors/problem-details';
import { useAuthStore, type UserProfile } from '@/core/auth/auth.store';

export interface LoginRequest {
  email: string;
  password: string;
  /** در ورود معمولاً undefined — انتخاب شرکت پس از login از memberships انجام می‌شود */
  tenantId?: string;
  companyId?: string;
}

export interface LoginResponse {
  accessToken: string;
  email: string;
  fullName: string;
  tenantId: string;
  companyId: string;
  permissions: string[];
}

export interface UserTenantDto {
  tenantId: string;
  tenantName: string;
  companies: UserCompanyDto[];
}

export interface UserCompanyDto {
  companyId: string;
  name: string;
  activeFiscalYear: number;
  isActive: boolean;
  membershipActive: boolean;
}

export interface MeResponse {
  userId: string | null;
  email: string | null;
  activeTenantId: string | null;
  activeCompanyId: string | null;
  roles: string[];
  permissions: string[];
}

export interface RefreshResponse {
  accessToken: string;
  tenantId: string;
  companyId: string;
  permissions: string[];
}

/** ورود — خطاهای امنیتی پیام یکسان دارند تا enumération ممکن نشود */
export async function login(request: LoginRequest): Promise<LoginResponse> {
  try {
    const { data } = await apiClient.post<LoginResponse>('/Auth/login', request);
    return data;
  } catch (error) {
    if (error instanceof ApiError) {
      // پیام backend دقیقاً حفظ می‌شود («نام کاربری یا کلمه عبور اشتباه است.»)
      throw error;
    }
    throw error;
  }
}

/** خروج: ابطال refresh token سمت سرور + پاک‌سازی حافظه فرانت */
export async function logout(): Promise<void> {
  try {
    await apiClient.post('/Auth/logout');
  } finally {
    useAuthStore.getState().clearAuth();
  }
}

/** پروفایل جاری از claims JWT */
export async function getCurrentUser(): Promise<UserProfile> {
  const { data } = await apiClient.get<MeResponse>('/Auth/me');
  return {
    userId: data.userId ?? '',
    email: data.email ?? '',
    fullName: data.email ?? '',
    roles: data.roles ?? [],
    permissions: data.permissions ?? [],
  };
}

/**
 * تمدید نشست با Cookie HttpOnly — بدون دسترسی جاوااسکریپت به refresh token.
 * از apiClient استفاده نمی‌شود تا در interceptorهای آن حلقه refresh شکل نگیرد.
 * هدرهای زمینه: تعویض شرکت بدون رمز عبور (backend membership را validate می‌کند).
 */
export async function refreshSession(options?: { tenantId?: string; companyId?: string }): Promise<RefreshResponse> {
  const headers: Record<string, string> = {};
  if (options?.tenantId) headers['X-Tenant-Id'] = options.tenantId;
  if (options?.companyId) headers['X-Company-Id'] = options.companyId;

  try {
    const { data } = await axios.post<RefreshResponse>(
      `${getApiBaseUrl()}/Auth/refresh-token`,
      {},
      { withCredentials: true, headers, timeout: 15000 },
    );
    return data;
  } catch (error) {
    // خطای refresh به ApiError با پیام/problem واقعی backend نگاشت می‌شود (403 عضویت و…)
    const axiosError = error as { response?: { status?: number; data?: { code?: string; detail?: string } }; message?: string };
    if (axiosError?.response) {
      const { status, data } = axiosError.response;
      throw new ApiError(status ?? 500, data?.detail || 'تمدید نشست ناموفق بود.', data?.code || `Http.${status}`, data);
    }
    throw new ApiError(0, 'خطا در برقراری ارتباط با سرور هنگام تمدید نشست.');
  }
}

/** لیست واقعی tenant/companies کاربر از memberships (GET /Auth/companies) */
export async function getUserCompanies(): Promise<UserTenantDto[]> {
  const { data } = await apiClient.get<UserTenantDto[]>('/Auth/companies');
  return data;
}

function getApiBaseUrl(): string {
  return (axios.defaults.baseURL as string) || 'https://localhost:5001/api/v1';
}
