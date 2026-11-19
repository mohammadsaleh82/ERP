import React from 'react';
import { useLocation } from 'react-router-dom';
import { routeTitles } from '@/app/config/menu';
import { useCompanyStore } from '@/core/company/company.store';
import { Breadcrumb as BreadcrumbShell, type BreadcrumbItem } from '@/shared/components/ui/breadcrumb';

export interface BreadcrumbProps {
  /** اگر داده شود مستقیم رندر می‌شود؛ وگرنه از مسیر جاری ساخته می‌شود */
  items?: BreadcrumbItem[];
  className?: string;
}

/** Breadcrumb از مسیر واقعی + نقشه عناوین — [شرکت] / [بخش] / [زیرمسیر] */
export const Breadcrumb: React.FC<BreadcrumbProps> = ({ items: explicitItems, className }) => {
  const location = useLocation();
  const { activeCompany } = useCompanyStore();

  const companyLabel = activeCompany?.name ?? 'خانه';

  const crumbs: BreadcrumbItem[] = explicitItems ?? [];
  if (!explicitItems) {
    const segments = location.pathname.split('/').filter(Boolean);
    let accumulated = '';
    segments.forEach((segment, index) => {
      accumulated += `/${segment}`;
      const isLast = index === segments.length - 1;
      const known = routeTitles[accumulated];
      if (known) {
        crumbs.push({ label: known, href: isLast ? undefined : accumulated });
      } else if (isLast) {
        crumbs.push({ label: segment });
      } else if (!/^[0-9a-f-]{8,}$/i.test(segment)) {
        crumbs.push({ label: segment, href: accumulated });
      }
    });
  }

  if (crumbs.length === 0) {
    return <BreadcrumbShell className={className} items={[{ label: companyLabel }]} />;
  }

  return <BreadcrumbShell className={className} items={[{ label: companyLabel }, ...crumbs]} />;
};
