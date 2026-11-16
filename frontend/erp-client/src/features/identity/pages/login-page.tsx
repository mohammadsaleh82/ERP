import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { Lock, Mail, ArrowLeft, Loader2 } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { useAuthStore } from '@/core/auth/auth.store';
import { toast } from '@/core/notifications/toast';

const loginSchema = z.object({
  email: z.string().email('آدرس ایمیل معتبر نیست.'),
  password: z.string().min(6, 'کلمه عبور باید حداقل ۶ کاراکتر باشد.'),
});

type LoginFormValues = z.infer<typeof loginSchema>;

export const LoginPage: React.FC = () => {
  const navigate = useNavigate();
  const { setAuth } = useAuthStore();
  const [isLoading, setIsLoading] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      email: '',
      password: '',
    },
  });

  const onSubmit = async (data: LoginFormValues) => {
    setIsLoading(true);
    try {
      // Mock login for skeleton phase
      await new Promise((resolve) => setTimeout(resolve, 800));

      setAuth(
        {
          id: 'u-1',
          email: data.email,
          fullName: 'مدیر ارشد مالی',
          roles: ['FinancialAdmin'],
          permissions: ['system:admin', 'accounting:vouchers:read'],
        },
        'mock-jwt-token'
      );

      toast.success('ورود با موفقیت انجام شد', 'به سامانه سازمانی ERP خوش آمدید.');
      navigate('/select-company');
    } catch {
      toast.error('خطا در احراز هویت', 'اطلاعات ورود اشتباه است.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <Card className="shadow-lg border-border">
      <CardHeader className="text-center space-y-2 pb-6">
        <div className="mx-auto w-12 h-12 rounded-xl bg-primary flex items-center justify-center text-primary-foreground font-black text-2xl shadow-md">
          E
        </div>
        <CardTitle className="text-xl font-bold">ورود به سامانه ERP</CardTitle>
        <CardDescription>جهت دسترسی به دفاتر مالی و سازمانی، وارد شوید</CardDescription>
      </CardHeader>

      <CardContent>
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div className="space-y-1.5">
            <label className="text-sm font-medium text-foreground">پست الکترونیکی</label>
            <div className="relative">
              <Input
                type="email"
                placeholder="name@company.com"
                dir="ltr"
                error={errors.email?.message}
                {...register('email')}
                className="ps-9"
              />
              <Mail className="absolute start-3 top-2.5 h-4 w-4 text-muted-foreground pointer-events-none" />
            </div>
          </div>

          <div className="space-y-1.5">
            <label className="text-sm font-medium text-foreground">کلمه عبور</label>
            <div className="relative">
              <Input
                type="password"
                placeholder="••••••••"
                dir="ltr"
                error={errors.password?.message}
                {...register('password')}
                className="ps-9"
              />
              <Lock className="absolute start-3 top-2.5 h-4 w-4 text-muted-foreground pointer-events-none" />
            </div>
          </div>

          <Button type="submit" className="w-full mt-2" disabled={isLoading}>
            {isLoading ? (
              <>
                <Loader2 className="w-4 h-4 animate-spin me-2" />
                در حال اعتبارسنجی...
              </>
            ) : (
              <>
                ورود به سامانه
                <ArrowLeft className="w-4 h-4 ms-2" />
              </>
            )}
          </Button>
        </form>
      </CardContent>
    </Card>
  );
};
