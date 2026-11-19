import React from 'react';
import { Outlet } from 'react-router-dom';
import { Sidebar } from '@/shared/components/layout/sidebar';
import { Header } from '@/shared/components/layout/header';
import { Breadcrumb } from '@/shared/components/layout/breadcrumb';

/**
 * Application Shell: Sidebar پایدار (دسکتاپ) + Header + Breadcrumb + Content.
 * در موبایل Sidebar جای خود را به Breadcrumb می‌دهد تا صفحه جا باز کند؛
 * Drawer موبایل در فاز بعد به Header اضافه می‌شود.
 */
export const AppLayout: React.FC = () => {
  return (
    <div className="flex min-h-screen bg-background">
      <div className="hidden md:block">
        <Sidebar />
      </div>
      <div className="flex min-w-0 flex-1 flex-col">
        <Header />
        <main className="flex-1 overflow-y-auto p-4 sm:p-6">
          <Breadcrumb />
          <div className="mt-4">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  );
};
