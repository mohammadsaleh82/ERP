export const env = {
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL || 'https://localhost:5001/api/v1',
  appName: import.meta.env.VITE_APP_NAME || 'سامانه جامع سازمانی ERP',
  appVersion: import.meta.env.VITE_APP_VERSION || '1.0.0',
  isDev: import.meta.env.DEV,
} as const;
