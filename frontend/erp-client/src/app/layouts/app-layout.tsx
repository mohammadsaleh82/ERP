import React from 'react';
import { Outlet } from 'react-router-dom';
import { Sidebar } from '@/shared/components/layout/sidebar';
import { Header } from '@/shared/components/layout/header';
import { Breadcrumb } from '@/shared/components/layout/breadcrumb';

/**
 * Application Shell (فاز ۹): Sidebar پایدار (دسکتاپ) + کشوی ناوبری موبایل در
 * Header + Breadcrumb + Content. skip-link و landmark اصلی با id پایدار برای
 * ناوبری کیبورد و screen reader.
 */
export const AppLayout: React.FC = () => (
  <div className="flex min-h-screen bg-background">
    {/* skip-link — اولین عنصر focusable صفحه */}
    <a href="#main-content" className="skip-link report-no-print">
      پرش به محتوای اصلی
    </a>
    <div className="hidden md:block">
      <Sidebar />
    </div>
    <div className="flex min-w-0 flex-1 flex-col">
      <Header />
      <main
        id="main-content"
        tabIndex={-1}
        className="flex-1 overflow-y-auto p-4 outline-none sm:p-6"
      >
        <Breadcrumb />
        <div className="mt-4">
          <Outlet />
        </div>
      </main>
    </div>
  </div>
);
