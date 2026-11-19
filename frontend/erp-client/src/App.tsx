import React from 'react';
import { RouterProvider } from 'react-router-dom';
import { AppProviders } from '@/app/providers/app-providers';
import { ErrorBoundary } from '@/app/errors/error-boundary';
import { BootGate } from '@/app/guards/boot-gate';
import { router } from '@/app/router';

export const App: React.FC = () => {
  return (
    <ErrorBoundary>
      <AppProviders>
        <BootGate>
          <RouterProvider router={router} />
        </BootGate>
      </AppProviders>
    </ErrorBoundary>
  );
};
