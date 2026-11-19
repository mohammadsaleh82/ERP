import * as React from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { ArrowRight, ShieldX } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';

interface AccessDeniedState {
  from?: string;
  permissions?: string[];
}

/** صفحه 403 — با نمایش مجوزهای لازم و مسیر بازگشت امن */
export const AccessDeniedPage: React.FC = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const state = (location.state as AccessDeniedState | null) ?? {};

  return (
    <div className="flex min-h-[60vh] flex-col items-center justify-center gap-4 rounded-xl border bg-surface p-8 text-center shadow-sm">
      <div className="flex size-14 items-center justify-center rounded-full bg-danger/10">
        <ShieldX className="size-8 text-danger" aria-hidden />
      </div>
      <h1 className="text-lg font-bold text-foreground">دسترسی غیرمجاز</h1>
      <p className="max-w-md text-sm leading-6 text-muted-foreground">
        {state.from
          ? `برای مشاهده «${state.from}» یکی از مجوزهای زیر لازم است:`
          : 'شما مجوز مشاهده این بخش را ندارید. با مدیر سیستم تماس بگیرید.'}
      </p>
      {state.permissions && state.permissions.length > 0 && (
        <ul className="max-w-md space-y-1 rounded-md border bg-muted/40 p-3 text-start">
          {state.permissions.map((permission) => (
            <li key={permission} dir="ltr" className="font-code text-xs text-muted-foreground">{permission}</li>
          ))}
        </ul>
      )}
      <Button variant="outline" onClick={() => navigate('/', { replace: true })}>
        <ArrowRight className="size-4" aria-hidden />
        بازگشت به داشبورد
      </Button>
    </div>
  );
};
