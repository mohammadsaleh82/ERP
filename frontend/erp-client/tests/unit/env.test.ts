import { describe, expect, it } from 'vitest';
import { env } from '@/app/config/env';
import { toast } from '@/core/notifications/toast';

describe('env config', () => {
  it('exposes a non-secret application configuration surface', () => {
    // هیچ Secret در سطح فرانت‌اند نباید وجود داشته باشد
    const keys = Object.keys(env);
    expect(keys).toEqual(['apiBaseUrl', 'appName', 'appVersion', 'isDev']);
  });

  it('defaults the API base URL to a versioned /api/v1 endpoint', () => {
    expect(env.apiBaseUrl).toMatch(/\/api\/v1$/);
  });
});

describe('notification facade', () => {
  it('exposes the four severity levels for the toast system', () => {
    expect(typeof toast.success).toBe('function');
    expect(typeof toast.error).toBe('function');
    expect(typeof toast.warning).toBe('function');
    expect(typeof toast.info).toBe('function');
  });
});
