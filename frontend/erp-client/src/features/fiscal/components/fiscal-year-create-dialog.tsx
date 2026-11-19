/** ایجاد سال مالی — CreateFiscalYearRequest واقعی؛ شروع/پایان اختیاری است (backend از تقویم می‌سازد) */
import * as React from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import * as fiscalApi from '@/core/accounting/fiscal.service';
import { ApiError } from '@/core/errors/problem-details';
import { calendarLabel, generationLabel } from '@/features/fiscal/fiscal-labels';
import { toast } from '@/core/notifications/toast';
import type { FiscalYearDto } from '@/core/accounting/fiscal.types';

const schema = z.object({
  yearNumber: z
    .number({ invalid_type_error: 'شماره سال الزامی است' })
    .int('شماره سال باید عدد صحیح باشد')
    .min(1300, 'شماره سال نامعتبر است')
    .max(1500, 'شماره سال نامعتبر است'),
  name: z.string().min(1, 'نام سال مالی الزامی است').max(100, 'نام طولانی است'),
  calendarType: z.enum(['Persian', 'Gregorian']),
  startDate: z.string().optional(),
  endDate: z.string().optional(),
  autoGeneratePeriods: z.enum(['Monthly', 'Quarterly', 'Custom']),
});

type FormValues = z.infer<typeof schema>;

export interface FiscalYearCreateDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: (year: FiscalYearDto) => void;
}

export const FiscalYearCreateDialog: React.FC<FiscalYearCreateDialogProps> = ({ open, onOpenChange, onSaved }) => {
  const {
    register,
    handleSubmit,
    reset,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { yearNumber: undefined as unknown as number, name: '', calendarType: 'Persian', startDate: '', endDate: '', autoGeneratePeriods: 'Monthly' },
  });

  React.useEffect(() => {
    if (open) reset({ yearNumber: undefined as unknown as number, name: '', calendarType: 'Persian', startDate: '', endDate: '', autoGeneratePeriods: 'Monthly' });
  }, [open, reset]);

  const calendarType = watch('calendarType');

  const onSubmit = handleSubmit(async (values) => {
    try {
      const year = await fiscalApi.createFiscalYear({
        yearNumber: values.yearNumber,
        name: values.name,
        calendarType: values.calendarType,
        startDate: values.startDate || null,
        endDate: values.endDate || null,
        autoGeneratePeriods: values.autoGeneratePeriods,
      });
      toast.success('سال مالی ایجاد شد', values.autoGeneratePeriods !== 'Custom' ? 'دوره‌های آن به‌صورت خودکار تولید شد.' : 'دوره‌ها را دستی بسازید.');
      onSaved(year);
      onOpenChange(false);
    } catch (error) {
      toast.error('ایجاد سال مالی انجام نشد', error instanceof ApiError ? error.message : 'پیش‌نیازها را بررسی کنید (مثلاً سال تکراری).');
      throw error;
    }
  });

  return (
    <Modal open={open} onOpenChange={onOpenChange} title="ایجاد سال مالی جدید" size="md">
      <form onSubmit={onSubmit} className="space-y-4" data-testid="fiscal-year-form">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <div>
            <label htmlFor="fy-number" className="mb-1 block text-xs font-semibold text-foreground">شماره سال *</label>
            <Input id="fy-number" inputMode="numeric" placeholder="1404" {...register('yearNumber', { valueAsNumber: true })} aria-invalid={Boolean(errors.yearNumber)} data-testid="fy-year-number" />
            {errors.yearNumber && <p className="mt-1 text-[11px] text-danger" role="alert">{errors.yearNumber.message}</p>}
          </div>
          <div>
            <label htmlFor="fy-name" className="mb-1 block text-xs font-semibold text-foreground">نام *</label>
            <Input id="fy-name" placeholder="سال مالی ۱۴۰۴" {...register('name')} aria-invalid={Boolean(errors.name)} data-testid="fy-name" />
            {errors.name && <p className="mt-1 text-[11px] text-danger" role="alert">{errors.name.message}</p>}
          </div>
          <div>
            <label htmlFor="fy-calendar" className="mb-1 block text-xs font-semibold text-foreground">تقویم مالی</label>
            <select
              id="fy-calendar"
              className="h-9 w-full rounded-md border border-border bg-surface px-2 text-sm"
              value={calendarType}
              onChange={(event) => setValue('calendarType', event.target.value as 'Persian' | 'Gregorian')}
              data-testid="fy-calendar"
            >
              <option value="Persian">{calendarLabel('Persian')}</option>
              <option value="Gregorian">{calendarLabel('Gregorian')}</option>
            </select>
          </div>
          <div>
            <label htmlFor="fy-generate" className="mb-1 block text-xs font-semibold text-foreground">دوره‌ها</label>
            <select id="fy-generate" className="h-9 w-full rounded-md border border-border bg-surface px-2 text-sm" {...register('autoGeneratePeriods')} data-testid="fy-generate">
              <option value="Monthly">{generationLabel('Monthly')}</option>
              <option value="Quarterly">{generationLabel('Quarterly')}</option>
              <option value="Custom">{generationLabel('Custom')}</option>
            </select>
          </div>
          <div>
            <label htmlFor="fy-start" className="mb-1 block text-xs font-semibold text-foreground">تاریخ شروع (اختیاری)</label>
            <Input id="fy-start" type="date" dir="ltr" {...register('startDate')} />
            <p className="mt-1 text-[10px] text-muted-foreground">خالی بمانید تا backend بر اساس تقویم محاسبه کند</p>
          </div>
          <div>
            <label htmlFor="fy-end" className="mb-1 block text-xs font-semibold text-foreground">تاریخ پایان (اختیاری)</label>
            <Input id="fy-end" type="date" dir="ltr" {...register('endDate')} />
          </div>
        </div>
        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>انصراف</Button>
          <Button type="submit" disabled={isSubmitting} data-testid="fy-submit">
            {isSubmitting ? 'در حال ایجاد…' : 'ایجاد سال مالی'}
          </Button>
        </div>
      </form>
    </Modal>
  );
};
