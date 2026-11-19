import * as React from 'react';
import { useNavigate } from 'react-router-dom';
import { LogOut, Mail, ShieldCheck, UserRound } from 'lucide-react';
import { Card, CardContent } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import { Avatar } from '@/shared/components/ui/avatar';
import { Button } from '@/shared/components/ui/button';
import { useAuthStore } from '@/core/auth/auth.store';
import { useCompanyStore } from '@/core/company/company.store';
import { logout } from '@/core/auth/auth.service';
import { queryClient } from '@/app/providers/query-provider';
import { digitsToPersian } from '@/shared/utils/number';

/** تنظیمات پروفایل — داده واقعی از GET /Auth/me (در auth store) */
export const ProfilePage: React.FC = () => {
  const navigate = useNavigate();
  const user = useAuthStore((state) => state.user);
  const { activeCompany, activeTenant } = useCompanyStore();

  const handleLogout = async () => {
    try {
      await logout();
    } finally {
      useCompanyStore.getState().clearCompanyContext();
      queryClient.clear();
      navigate('/login', { replace: true });
    }
  };

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <h1 className="text-xl font-bold">پروفایل کاربر</h1>

      <Card>
        <CardContent className="flex flex-wrap items-center gap-4 p-5">
          <Avatar name={user?.fullName || user?.email || 'کاربر'} size="lg" />
          <div className="min-w-0 flex-1">
            <p className="truncate text-base font-bold text-foreground">{user?.fullName || 'کاربر سیستم'}</p>
            <p className="flex items-center gap-1.5 text-sm text-muted-foreground" dir="ltr">
              <Mail className="size-3.5 shrink-0" aria-hidden />
              {user?.email}
            </p>
          </div>
          <Button variant="destructive" onClick={() => void handleLogout()}>
            <LogOut className="size-4" aria-hidden />
            خروج از حساب
          </Button>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-5">
          <p className="mb-3 flex items-center gap-2 text-sm font-bold">
            <UserRound className="size-4 text-muted-foreground" aria-hidden />
            زمینه کاری فعال
          </p>
          <dl className="grid gap-3 sm:grid-cols-2">
            <div className="rounded-md border p-3">
              <dt className="text-xs text-muted-foreground">شرکت فعال</dt>
              <dd className="mt-1 text-sm font-semibold">{activeCompany?.name ?? '—'}</dd>
            </div>
            <div className="rounded-md border p-3">
              <dt className="text-xs text-muted-foreground">سازمان (Tenant)</dt>
              <dd className="mt-1 text-sm font-semibold">{activeTenant?.name ?? '—'}</dd>
            </div>
            <div className="rounded-md border p-3">
              <dt className="text-xs text-muted-foreground">سال مالی فعال</dt>
              <dd className="mt-1 text-sm font-semibold">{activeCompany ? digitsToPersian(activeCompany.fiscalYear) : '—'}</dd>
            </div>
            <div className="rounded-md border p-3">
              <dt className="text-xs text-muted-foreground">شناسه کاربر</dt>
              <dd className="font-code mt-1 text-xs">{user?.userId || '—'}</dd>
            </div>
          </dl>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-5">
          <p className="mb-3 flex items-center gap-2 text-sm font-bold">
            <ShieldCheck className="size-4 text-muted-foreground" aria-hidden />
            نقش‌ها و مجوزها
          </p>
          <div className="mb-4 flex flex-wrap gap-1.5">
            {(user?.roles ?? []).length === 0 ? (
              <span className="text-xs text-muted-foreground">بدون نقش اختصاصی</span>
            ) : (
              user?.roles.map((role) => (
                <Badge key={role} variant="default">{role}</Badge>
              ))
            )}
          </div>
          <p className="mb-2 text-xs text-muted-foreground">مجوزهای فعال شرکت جاری:</p>
          <ul className="grid gap-1.5 sm:grid-cols-2">
            {(user?.permissions ?? []).map((permission) => (
              <li key={permission} dir="ltr" className="font-code rounded border bg-muted/30 px-2 py-1 text-xs text-foreground">
                {permission}
              </li>
            ))}
            {(user?.permissions ?? []).length === 0 && (
              <li className="text-xs text-muted-foreground">مجوزی ثبت نشده است</li>
            )}
          </ul>
        </CardContent>
      </Card>
    </div>
  );
};
