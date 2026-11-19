import axios, { AxiosError, InternalAxiosRequestConfig } from 'axios';
import { env } from '@/app/config/env';
import { useAuthStore } from '@/core/auth/auth.store';
import { useCompanyStore } from '@/core/company/company.store';
import { queryClient } from '@/app/providers/query-provider';
import { ApiError, ProblemDetails } from '@/core/errors/problem-details';
import type { RefreshResponse } from '@/core/auth/auth.service';

export const apiClient = axios.create({
  baseURL: env.apiBaseUrl,
  timeout: 30000,
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json',
    Accept: 'application/json',
  },
});

/** mutex تمدید نشست — همه 401های هم‌زمان فقط یک refresh منتظر می‌مانند */
let isRefreshing = false;
let failedQueue: Array<{
  resolve: (token: string) => void;
  reject: (reason?: unknown) => void;
}> = [];

const processQueue = (error: unknown, token?: string) => {
  failedQueue.forEach((promise) => {
    if (error) {
      promise.reject(error);
    } else if (token !== undefined) {
      promise.resolve(token);
    }
  });
  failedQueue = [];
};

apiClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const { accessToken, activeTenantId } = useAuthStore.getState();
    const { activeTenant, activeCompany, activeBranchId } = useCompanyStore.getState();
    if (accessToken) {
      config.headers.Authorization = `Bearer ${accessToken}`;
    }
    // هدرهای زمینه — اولویت store فعال؛ fallback به زمینه JWT نشست
    const tenantId = activeTenant?.id ?? activeTenantId;
    if (tenantId) {
      config.headers['X-Tenant-Id'] = tenantId;
    }
    if (activeCompany?.id) {
      config.headers['X-Company-Id'] = activeCompany.id;
    }
    if (activeBranchId) {
      config.headers['X-Branch-Id'] = activeBranchId;
    }
    return config;
  },
  (error) => Promise.reject(error),
);

/** پاک‌سازی کامل داده نشست — بدون نشت به مسیر بعدی */
function terminateSession(): void {
  useAuthStore.getState().clearAuth();
  useCompanyStore.getState().clearCompanyContext();
  // cache وابسته به شرکت/کاربر کامل پاک می‌شود تا داده شرکت قبلی نمایش داده نشود
  queryClient.clear();
}

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as (InternalAxiosRequestConfig & { _retry?: boolean }) | undefined;

    if (!error.response) {
      return Promise.reject(new ApiError(0, 'خطا در برقراری ارتباط با سرور. لطفا اتصال اینترنت را بررسی کنید.'));
    }

    const status = error.response.status;
    const data = error.response.data as ProblemDetails | undefined;

    // خطای اعتبارات login مستقیم به caller برمی‌گردد — بدون تلاش refresh (ضد-حلقه)
    const requestUrl = originalRequest?.url ?? '';
    const isAuthEndpoint = requestUrl.includes('/Auth/login') || requestUrl.includes('/Auth/refresh-token');

    // refresh خودش با apiClient صدا زده نمی‌شود (axios خام) → حلقه refresh ناممکن است
    if (status === 401 && originalRequest && !originalRequest._retry && !isAuthEndpoint) {
      if (isRefreshing) {
        // درخواست‌های بعدی تا پایان refresh در صف می‌مانند
        return new Promise<string>((resolve, reject) => {
          failedQueue.push({ resolve, reject });
        }).then((token) => {
          originalRequest.headers.Authorization = `Bearer ${token}`;
          return apiClient(originalRequest);
        });
      }

      originalRequest._retry = true;
      isRefreshing = true;

      try {
        const { data: refreshed } = await axios.post<RefreshResponse>(
          `${env.apiBaseUrl}/Auth/refresh-token`,
          {},
          { withCredentials: true, timeout: 15000 },
        );

        // sync کامل زمینه جدید (تعویض شرکت در refresh هم ممکن است)
        useAuthStore.getState().setSession(
          refreshed.accessToken,
          refreshed.tenantId,
          refreshed.companyId,
          refreshed.permissions,
        );
        processQueue(null, refreshed.accessToken);

        originalRequest.headers.Authorization = `Bearer ${refreshed.accessToken}`;
        return apiClient(originalRequest);
      } catch (refreshError: unknown) {
        processQueue(refreshError);
        terminateSession();
        return Promise.reject(
          new ApiError(401, 'نشست کاربری شما منقضی شده است. لطفا مجددا وارد شوید.', 'Auth.SessionExpired'),
        );
      } finally {
        isRefreshing = false;
      }
    }

    // خطای دسترسی — بدون retry؛ پیام backend (problem+json فارسی) حفظ می‌شود
    if (status === 403) {
      return Promise.reject(
        new ApiError(403, data?.detail || 'شما به این بخش دسترسی ندارید.', data?.code || 'Auth.Forbidden', data),
      );
    }

    const message = data?.detail || error.message || 'خطای غیرمنتظره در سرور رخ داده است.';
    const code = data?.code || `Http.${status}`;
    return Promise.reject(new ApiError(status, message, code, data));
  },
);
