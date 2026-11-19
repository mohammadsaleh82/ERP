import '@testing-library/jest-dom/vitest';
import { afterEach } from 'vitest';
import { cleanup } from '@testing-library/react';

// با globals:false پاک‌سازی خودکار RTL فعال نیست — بعد از هر تست unmount می‌کنیم
afterEach(() => {
  cleanup();
});
