import React from 'react';
import { useNavigate } from 'react-router-dom';
import { ShieldAlert, ArrowRight } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';

export const AccessDeniedPage: React.FC = () => {
  const navigate = useNavigate();

  return (
    <div className="min-h-[70vh] flex flex-col items-center justify-center text-center p-6 space-y-4">
      <div className="w-16 h-16 rounded-full bg-danger/10 flex items-center justify-center text-danger">
        <ShieldAlert className="w-8 h-8" />
      </div>
      <h1 className="text-3xl font-bold tracking-tight text-foreground">دسترسی مسدود است (403)</h1>
      <p className="text-sm text-muted-foreground max-w-md">
        شما مجوز لازم برای مشاهده این بخش یا انجام عملیات در شرکت جاری را ندارید. در صورت نیاز با مدیر ارشد سیستم تماس بگیرید.
      </p>
      <Button variant="outline" onClick={() => navigate('/')} className="mt-2">
        <ArrowRight className="w-4 h-4 me-2" />
        بازگشت به داشبورد
      </Button>
    </div>
  );
};
