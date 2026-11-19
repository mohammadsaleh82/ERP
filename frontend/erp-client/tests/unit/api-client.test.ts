import { afterEach, describe, expect, it } from 'vitest';
import { AxiosError, AxiosHeaders, type InternalAxiosRequestConfig } from 'axios';
import { apiClient } from '@/core/api/client';
import { useAuthStore } from '@/core/auth/auth.store';
import { ApiError } from '@/core/errors/problem-details';

type ResponseHandlerEntry = (typeof apiClient.interceptors.response.handlers)[number];
type RequestHandlerEntry = (typeof apiClient.interceptors.request.handlers)[number];

const getResponseRejectionHandler = (): NonNullable<ResponseHandlerEntry['rejected']> => {
  const handler = apiClient.interceptors.response.handlers[0];
  if (!handler?.rejected) throw new Error('response rejection interceptor missing');
  return handler.rejected;
};

const buildConfig = (): InternalAxiosRequestConfig => ({
  headers: new AxiosHeaders(),
});

const buildBackendError = (status: number, data: unknown): AxiosError =>
  new AxiosError('Request failed', undefined, buildConfig(), undefined, {
    status,
    data,
  } as never);

describe('api client interceptors', () => {
  afterEach(() => {
    useAuthStore.getState().clearAuth();
  });

  it('attaches the bearer token when authenticated', async () => {
    const handler = apiClient.interceptors.request.handlers[0];
    if (!handler?.fulfilled) throw new Error('request interceptor missing');
    const fulfilled = handler.fulfilled as NonNullable<RequestHandlerEntry['fulfilled']>;

    useAuthStore.getState().setAccessToken('jwt-token');
    const config = await fulfilled(buildConfig());
    expect(config.headers.get('Authorization')).toBe('Bearer jwt-token');
  });

  it('does not attach an Authorization header for anonymous requests', async () => {
    const handler = apiClient.interceptors.request.handlers[0];
    if (!handler?.fulfilled) throw new Error('request interceptor missing');
    const fulfilled = handler.fulfilled as NonNullable<RequestHandlerEntry['fulfilled']>;

    const config = await fulfilled(buildConfig());
    expect(config.headers.get('Authorization')).toBeUndefined();
  });

  it('maps backend ProblemDetails into a typed ApiError', async () => {
    const rejected = getResponseRejectionHandler();
    const error = await rejected(buildBackendError(400, {
      detail: 'مبلغ سند نامعتبر است',
      code: 'Voucher.Unbalanced',
    })).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    const apiError = error as ApiError;
    expect(apiError.status).toBe(400);
    expect(apiError.message).toBe('مبلغ سند نامعتبر است');
    expect(apiError.code).toBe('Voucher.Unbalanced');
  });

  it('falls back to Http.<status> code when backend has no code', async () => {
    const rejected = getResponseRejectionHandler();
    const error = await rejected(buildBackendError(404, { title: 'یافت نشد' })).catch(
      (e: unknown) => e,
    );

    const apiError = error as ApiError;
    expect(apiError.code).toBe('Http.404');
  });

  it('converts network failures into a friendly offline ApiError', async () => {
    const rejected = getResponseRejectionHandler();
    const networkError = new AxiosError('Network Error', undefined, buildConfig());
    const error = await rejected(networkError).catch((e: unknown) => e);

    const apiError = error as ApiError;
    expect(apiError.status).toBe(0);
    expect(apiError.message).toContain('اتصال');
  });
});
