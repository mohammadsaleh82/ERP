import axios, { AxiosError, InternalAxiosRequestConfig } from 'axios';
import { env } from '@/app/config/env';
import { useAuthStore } from '@/core/auth/auth.store';
import { useCompanyStore } from '@/core/company/company.store';
import { ApiError, ProblemDetails } from '@/core/errors/problem-details';

export const apiClient = axios.create({
  baseURL: env.apiBaseUrl,
  timeout: 30000,
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json',
    Accept: 'application/json',
  },
});

let isRefreshing = false;
let failedQueue: Array<{
  resolve: (value?: unknown) => void;
  reject: (reason?: unknown) => void;
}> = [];

const processQueue = (error: unknown, token: string | null = null) => {
  failedQueue.forEach((promise) => {
    if (error) {
      promise.reject(error);
    } else {
      promise.resolve(token);
    }
  });
  failedQueue = [];
};

apiClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const accessToken = useAuthStore.getState().accessToken;
    if (accessToken) {
      config.headers.Authorization = `Bearer ${accessToken}`;
    }

    const { activeTenant, activeCompany, activeBranchId } = useCompanyStore.getState();
    if (activeTenant?.id) {
      config.headers['X-Tenant-Id'] = activeTenant.id;
    }
    if (activeCompany?.id) {
      config.headers['X-Company-Id'] = activeCompany.id;
    }
    if (activeBranchId) {
      config.headers['X-Branch-Id'] = activeBranchId;
    }

    return config;
  },
  (error) => Promise.reject(error)
);

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean };

    if (!error.response) {
      return Promise.reject(new ApiError(0, 'خطا در برقراری ارتباط با سرور. لطفا اتصال اینترنت را بررسی کنید.'));
    }

    const status = error.response.status;
    const data = error.response.data as ProblemDetails;

    // Handle 401 Unauthorized with token refresh mutex
    if (status === 401 && originalRequest && !originalRequest._retry) {
      if (isRefreshing) {
        return new Promise((resolve, reject) => {
          failedQueue.push({ resolve, reject });
        })
          .then((token) => {
            if (originalRequest.headers) {
              originalRequest.headers.Authorization = `Bearer ${token}`;
            }
            return apiClient(originalRequest);
          })
          .catch((err) => Promise.reject(err));
      }

      originalRequest._retry = true;
      isRefreshing = true;

      try {
        const refreshResponse = await axios.post<{ accessToken: string }>(
          `${env.apiBaseUrl}/identity/refresh-token`,
          {},
          { withCredentials: true }
        );

        const newAccessToken = refreshResponse.data.accessToken;
        useAuthStore.getState().setAccessToken(newAccessToken);

        processQueue(null, newAccessToken);

        if (originalRequest.headers) {
          originalRequest.headers.Authorization = `Bearer ${newAccessToken}`;
        }
        return apiClient(originalRequest);
      } catch (refreshErr) {
        processQueue(refreshErr, null);
        useAuthStore.getState().clearAuth();
        return Promise.reject(new ApiError(401, 'نشست کاربری شما منقضی شده است. لطفا مجددا وارد شوید.', 'Auth.SessionExpired'));
      } finally {
        isRefreshing = false;
      }
    }

    const message = data?.detail || error.message || 'خطای غیرمنتظره در سرور رخ داده است.';
    const code = data?.code || `Http.${status}`;

    return Promise.reject(new ApiError(status, message, code, data));
  }
);
