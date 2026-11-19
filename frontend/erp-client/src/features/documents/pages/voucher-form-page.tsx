/** صفحه ایجاد سند جدید — پوسته فرم با بازگشت به جزئیات پس از ذخیره */
import * as React from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowRight } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { VoucherForm } from '@/features/documents/components/voucher-form';

export const VoucherFormPage: React.FC = () => {
  const navigate = useNavigate();

  return (
    <div className="space-y-4" data-testid="voucher-form-page">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="icon" onClick={() => navigate('/accounting/documents')} aria-label="بازگشت به لیست">
          <ArrowRight className="size-4" aria-hidden />
        </Button>
        <h1 className="text-lg font-black text-foreground">ایجاد سند حسابداری</h1>
      </div>
      <VoucherForm onSaved={(documentId) => navigate(`/accounting/documents/${documentId}`)} />
    </div>
  );
};
