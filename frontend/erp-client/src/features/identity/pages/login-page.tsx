import * as React from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useLocation, useNavigate } from 'react-router-dom';
import { AlertCircle, Building2, LogIn } from 'lucide-react';
import { Input } from '@/shared/components/ui/input';
import { PasswordInput } from '@/shared/components/forms/password-input';
import { Button } from '@/shared/components/ui/button';
import { FormField } from '@/shared/components/forms/form-field';
import { useAuthStore } from '@/core/auth/auth.store';
import { login, getUserCompanies } from '@/core/auth/auth.service';
import { useCompanyStore } from '@/core/company/company.store';
import { ApiError } from '@/core/errors/problem-details';
import { env } from '@/app/config/env';

const loginSchema = z.object({
  email: z.string().min(1, 'ایمیل الزامی است').email('فرمت ایمیل صحیح نیست'),
  password: z.string().min(1, 'کلمه عبور الزامی است'),
});

type LoginFormData = z.infer<typeof loginSchema>;

/** صفحه ورود با قرارداد واقعی backend — پیام خطای امن و یکسان برای اعتبارات غلط */
export const LoginPage: React.FC = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const setAuth = useAuthStore((state) => state.setAuth);
  const setTenants = useCompanyStore((state) => state.setTenants);
  const [formError, setFormError] = React.useState<string | null>(null);
  /* فاز ۹ — پس از خطای ورود، فوکوس به پیام خطا می‌رود تا screen reader/کیبورد آن را دریافت کند */
  const alertRef = React.useRef<HTMLDivElement>(null);
  React.useEffect(() => {
    if (formError) alertRef.current?.focus();
  }, [formError]);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  });

  const from = (location.state as { from?: string } | null)?.from;

  const onSubmit = async (values: LoginFormData) => {
    setFormError(null);
    try {
      const response = await login(values);
      // لیست واقعی شرکت‌ها — مبنای صفحه انتخاب شرکت
      try {
        const companies = await getUserCompanies();
        setTenants(companies);
      } catch {
        setTenants([]);
      }

      setAuth(
        { userId: '', email: response.email, fullName: response.fullName, roles: [], permissions: response.permissions },
        response.accessToken,
        response.tenantId,
        response.companyId,
      );
      // پروفایل کامل پس از ورود (userId/roles) — me endpoint
      try {
        const { getCurrentUser } = await import('@/core/auth/auth.service');
        const profile = await getCurrentUser();
        useAuthStore.getState().setSession(response.accessToken, response.tenantId, response.companyId, response.permissions);
        useAuthStore.setState({
          user: {
            userId: profile.userId,
            email: profile.email,
            fullName: response.fullName || profile.email,
            roles: profile.roles,
            permissions: profile.permissions,
          },
        });
      } catch {
        /* پروفایل غیرحیاتی است؛ ورود باقی می‌ماند */
      }

      navigate(from || '/select-company', { replace: true });
    } catch (error) {
      if (error instanceof ApiError) {
        setFormError(error.message);
      } else {
        setFormError('خطای غیرمنتظره در ورود. لطفا دوباره تلاش کنید.');
      }
    }
  };

  return (
    <div className="flex flex-col gap-6 rounded-xl border bg-surface p-6 shadow-lg">
      <div className="flex flex-col items-center gap-2 text-center">
        <div className="flex size-12 items-center justify-center rounded-xl bg-primary text-primary-foreground">
          <Building2 className="size-6" aria-hidden />
        </div>
        <h1 className="text-lg font-bold text-foreground">ورود به {env.appName}</h1>
        <p className="text-sm text-muted-foreground">با حساب سازمانی خود وارد شوید</p>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
        {formError && (
          <div ref={alertRef} tabIndex={-1} role="alert" className="flex items-start gap-2 rounded-md border border-danger/30 bg-danger/5 px-3 py-2.5 text-xs font-medium text-danger outline-none">
            <AlertCircle className="mt-0.5 size-4 shrink-0" aria-hidden />
            <span>{formError}</span>
          </div>
        )}

        <FormField id="login-email" label="ایمیل سازمانی" required error={errors.email?.message}>
          <Input
            id="login-email"
            type="email"
            dir="ltr"
            autoComplete="username"
            placeholder="name@company.ir"
            aria-invalid={Boolean(errors.email)}
            {...register('email')}
          />
        </FormField>

        <FormField id="login-password" label="کلمه عبور" required error={errors.password?.message}>
          <PasswordInput
            id="login-password"
            autoComplete="current-password"
            placeholder="••••••••"
            aria-invalid={Boolean(errors.password)}
            {...register('password')}
          />
        </FormField>

        <Button type="submit" className="mt-1 w-full" loading={isSubmitting}>
          {!isSubmitting && <LogIn className="size-4" aria-hidden />}
          {isSubmitting ? 'در حال ورود…' : 'ورود'}
        </Button>
      </form>

      <p className="text-center text-xs text-muted-foreground">
        نشست شما با توکن امن HttpOnly محافظت می‌شود؛ دسترسی‌ها بر اساس نقش سازمانی تعیین می‌شوند.
      </p>
    </div>
  );
};
