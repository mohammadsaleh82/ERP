/** دوره سفارشی — CreateCustomPeriodRequest واقعی (شماره، نام، بازه، تعدیلات) */
import * as React from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Switch } from '@/shared/components/ui/selection-controls';
import * as fiscalApi from '@/core/accounting/fiscal.service';
import { ApiError } from '@/core/errors/problem-details';
import { toast } from '@/core/notifications/toast';
import type { FiscalYearDto } from '@/core/accounting/fiscal.types';

const schema = z
  .object({
    periodNumber: z.number({ invalid_type_error: 'شماره دوره الزامی است' }).int().min(1, 'حداقل ۱').max(99, 'حداکثر ۹۹'),
    name: z.string().min(1, 'نام دوره الزامی است'),
    startDate: z.string().min(1, 'تاریخ شروع الزامی است'),
    endDate: z.string().min(1, 'تاریخ پایان الزامی است'),
    isAdjustmentPeriod: z.boolean(),
  })
  .refine((values) => !values.startDate || !values.endDate || values.startDate < values.endDate, {
    message: 'تاریخ پایان باید بعد از شروع باشد',
    path: ['endDate'],
  });

type FormValues = z.infer<typeof schema>;

export interface CustomPeriodDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  year: FiscalYearDto | null;
  onDone: () => void;
}

export const CustomPeriodDialog: React.FC<CustomPeriodDialogProps> = ({ open, onOpenChange, year, onDone }) => {
  const {
    register,
    handleSubmit,
    reset,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { periodNumber: undefined as unknown as number, name: '', startDate: '', endDate: '', isAdjustmentPeriod: false },
  });

  React.useEffect(() => {
    if (open) reset({ periodNumber: undefined as unknown as number, name: '', startDate: '', endDate: '', isAdjustmentPeriod: false });
  }, [open, reset]);

  const onSubmit = handleSubmit(async (values) => {
    try {
      const period = await fiscalApi.createCustomPeriod(year!.id, {
        periodNumber: values.periodNumber,
        name: values.name,
        startDate: values.startDate,
        endDate: values.endDate,
        isAdjustmentPeriod: values.isAdjustmentPeriod,
      });
      toast.success('دوره سفارشی ایجاد شد', period.name);
      onDone();
      onOpenChange(false);
    } catch (error) {
      toast.error('ایجاد دوره انجام نشد', error instanceof ApiError ? error.message : 'بازه با دوره‌های موجود هم‌پوشانی دارد.');
      throw error;
    }
  });

  return (
    <Modal open={open} onOpenChange={onOpenChange} title="ایجاد دوره سفارشی" size="sm">
      <form onSubmit={onSubmit} className="space-y-3" data-testid="custom-period-form">
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label htmlFor="cp-number" className="mb-1 block text-xs font-semibold text-foreground">شماره دوره *</label>
            <Input id="cp-number" inputMode="numeric" {...register('periodNumber', { valueAsNumber: true })} aria-invalid={Boolean(errors.periodNumber)} data-testid="cp-number" />
            {errors.periodNumber && <p className="mt-1 text-[11px] text-danger" role="alert">{errors.periodNumber.message}</p>}
          </div>
          <div>
            <label htmlFor="cp-name" className="mb-1 block text-xs font-semibold text-foreground">نام *</label>
            <Input id="cp-name" placeholder="مثلاً تعدیلات پایان سال" {...register('name')} aria-invalid={Boolean(errors.name)} data-testid="cp-name" />
            {errors.name && <p className="mt-1 text-[11px] text-danger" role="alert">{errors.name.message}</p>}
          </div>
          <div>
            <label htmlFor="cp-start" className="mb-1 block text-xs font-semibold text-foreground">شروع *</label>
            <Input id="cp-start" type="date" dir="ltr" {...register('startDate')} />
            {errors.startDate && <p className="mt-1 text-[11px] text-danger" role="alert">{errors.startDate.message}</p>}
          </div>
          <div>
            <label htmlFor="cp-end" className="mb-1 block text-xs font-semibold text-foreground">پایان *</label>
            <Input id="cp-end" type="date" dir="ltr" {...register('endDate')} />
            {errors.endDate && <p className="mt-1 text-[11px] text-danger" role="alert">{errors.endDate.message}</p>}
          </div>
        </div>
        <Switch
          checked={watch('isAdjustmentPeriod')}
          onCheckedChange={(checked) => setValue('isAdjustmentPeriod', checked)}
          label="دوره تعدیلات (خارج از گردش عادی)"
          data-testid="cp-adjustment"
        />
        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>انصراف</Button>
          <Button type="submit" disabled={isSubmitting}>{isSubmitting ? 'در حال ثبت…' : 'ایجاد دوره'}</Button>
        </div>
      </form>
    </Modal>
  );
};
