/**
 * منوی اصلی با مجوز واقعی هر بخش — ماتریس مجوزهای backend.
 * منو فقط نمایش را کنترل می‌کند؛ اعمال نهایی مجوز در backend است.
 */
import {
  LayoutDashboard,
  Receipt,
  Landmark,
  Boxes,
  ShoppingCart,
  TrendingUp,
  Users2,
  BadgeDollarSign,
  BarChart3,
  Building2,
  ShieldCheck,
  Settings,
} from 'lucide-react';
import { PERMISSIONS } from '@/core/auth/permissions';

export interface MenuItem {
  label: string;
  href: string;
  icon: React.ElementType;
  /** حداقل یکی از مجوزها برای نمایش آیتم منو؛ undefined = عمومی */
  permissions?: readonly string[];
}

export const menuItems: MenuItem[] = [
  { label: 'داشبورد مدیریتی', href: '/', icon: LayoutDashboard },
  { label: 'حسابداری مالی', href: '/accounting', icon: Receipt, permissions: [PERMISSIONS.documentsRead, PERMISSIONS.accountsRead] },
  { label: 'خزانه‌داری و چک', href: '/treasury', icon: Landmark, permissions: [PERMISSIONS.accountsRead] },
  { label: 'انبار و موجودی', href: '/inventory', icon: Boxes },
  { label: 'فروش و بازرگانی', href: '/sales', icon: TrendingUp },
  { label: 'خرید و تدارکات', href: '/purchasing', icon: ShoppingCart },
  { label: 'طرف‌حساب‌ها', href: '/parties', icon: Users2 },
  { label: 'حقوق و دستمزد', href: '/payroll', icon: BadgeDollarSign },
  { label: 'گزارش‌های جامع', href: '/reporting', icon: BarChart3, permissions: [PERMISSIONS.reportsRead, PERMISSIONS.ledgerRead] },
  { label: 'ساختار سازمان', href: '/organization', icon: Building2 },
  { label: 'کاربران و امنیت', href: '/identity', icon: ShieldCheck },
  { label: 'تنظیمات و پلن‌ها', href: '/settings', icon: Settings },
];

/** نقشه مسیر → عنوان برای Breadcrumb (مسیرهای عمیق با suffix/:id عمومی می‌شوند) */
export const routeTitles: Record<string, string> = {
  '/': 'داشبورد مدیریتی',
  '/accounting': 'حسابداری مالی',
  '/treasury': 'خزانه‌داری و چک',
  '/inventory': 'انبار و موجودی',
  '/sales': 'فروش و بازرگانی',
  '/purchasing': 'خرید و تدارکات',
  '/parties': 'طرف‌حساب‌ها',
  '/payroll': 'حقوق و دستمزد',
  '/reporting': 'گزارش‌های جامع',
  '/organization': 'ساختار سازمان',
  '/identity': 'کاربران و امنیت',
  '/settings': 'تنظیمات و پلن‌ها',
  '/profile': 'پروفایل کاربر',
  '/design-system': 'Design System',
  '/select-company': 'انتخاب شرکت',
  '/access-denied': 'دسترسی غیرمجاز',
};
