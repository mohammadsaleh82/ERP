import React from 'react';
import { useNavigate } from 'react-router-dom';
import { FileQuestion, ArrowRight } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';

export const NotFoundPage: React.FC = () => {
  const navigate = useNavigate();

  return (
    <div className="min-h-[70vh] flex flex-col items-center justify-center text-center p-6 space-y-4">
      <div className="w-16 h-16 rounded-full bg-muted flex items-center justify-center text-muted-foreground">
        <FileQuestion className="w-8 h-8" />
      </div>
      <h1 className="text-3xl font-bold tracking-tight text-foreground">صفحه مورد نظر یافت نشد (404)</h1>
      <p className="text-sm text-muted-foreground max-w-md">
        آدرسی که وارد کرده‌اید وجود ندارد یا ممکن است به بخش دیگری منتقل شده باشد.
      </p>
      <Button onClick={() => navigate('/')} className="mt-2">
        <ArrowRight className="w-4 h-4 me-2" />
        بازگشت به صفحه اصلی
      </Button>
    </div>
  );
};
