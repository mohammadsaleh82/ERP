import { describe, expect, it } from 'vitest';
import { ApiError, type ProblemDetails } from '@/core/errors/problem-details';

describe('ApiError / ProblemDetails contract', () => {
  it('carries status, message and default code', () => {
    const error = new ApiError(500, 'خطای سرور');
    expect(error.status).toBe(500);
    expect(error.message).toBe('خطای سرور');
    expect(error.code).toBe('Api.Error');
    expect(error.name).toBe('ApiError');
    expect(error instanceof Error).toBe(true);
  });

  it('keeps ProblemDetails payload for structured backend errors', () => {
    const problem: ProblemDetails = {
      type: 'https://erp.example/errors/validation',
      title: 'اعتبارسنجی ناموفق',
      status: 400,
      detail: 'مبلغ سند نامعتبر است',
      code: 'Voucher.Unbalanced',
      errors: { amount: ['مبلغ باید بزرگ‌تر از صفر باشد'] },
    };
    const error = new ApiError(400, problem.detail ?? '', problem.code, problem);
    expect(error.code).toBe('Voucher.Unbalanced');
    expect(error.problemDetails?.errors?.amount).toEqual(['مبلغ باید بزرگ‌تر از صفر باشد']);
  });

  it('preserves the session-expired contract used by the api client', () => {
    const error = new ApiError(401, 'نشست کاربری شما منقضی شده است.', 'Auth.SessionExpired');
    expect(error.status).toBe(401);
    expect(error.code).toBe('Auth.SessionExpired');
  });
});
