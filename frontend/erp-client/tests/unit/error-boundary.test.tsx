import { afterAll, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ErrorBoundary } from '@/app/errors/error-boundary';

// jsdom اجازه spy روی window.location.assign را نمی‌دهد؛ یک جایگزین قابل نوشتن نصب می‌کنیم
const realLocation = window.location;
Object.defineProperty(window, 'location', {
  configurable: true,
  writable: true,
  value: { ...realLocation, assign: vi.fn() },
});
afterAll(() => {
  Object.defineProperty(window, 'location', { configurable: true, writable: true, value: realLocation });
});

const Boom: React.FC = () => {
  throw new Error('خطای شبیه‌سازی‌شده رندر');
};

describe('ErrorBoundary', () => {
  it('renders children when no error is thrown', () => {
    render(
      <ErrorBoundary>
        <div>محتوای سالم</div>
      </ErrorBoundary>,
    );
    expect(screen.getByText('محتوای سالم')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('renders the fallback alert when a child throws during render', () => {
    const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => undefined);

    render(
      <ErrorBoundary>
        <Boom />
      </ErrorBoundary>,
    );

    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByText('خطای غیرمنتظره رخ داد')).toBeInTheDocument();
    consoleSpy.mockRestore();
  });

  it('offers a recovery action that navigates home', async () => {
    const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const assignSpy = vi.mocked(window.location.assign);

    render(
      <ErrorBoundary>
        <Boom />
      </ErrorBoundary>,
    );

    await userEvent.click(screen.getByRole('button', { name: /بازگشت به صفحه اصلی/ }));
    expect(assignSpy).toHaveBeenCalledWith('/');

    vi.mocked(window.location.assign).mockClear();
    consoleSpy.mockRestore();
  });
});
