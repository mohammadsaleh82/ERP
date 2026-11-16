import React from 'react';
import { Toaster } from 'sonner';
import { QueryProvider } from './query-provider';

interface AppProvidersProps {
  children: React.ReactNode;
}

export const AppProviders: React.FC<AppProvidersProps> = ({ children }) => {
  return (
    <QueryProvider>
      {children}
      <Toaster
        dir="rtl"
        position="top-left"
        richColors
        closeButton
        toastOptions={{
          style: {
            fontFamily: 'var(--font-sans)',
          },
        }}
      />
    </QueryProvider>
  );
};
