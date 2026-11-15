import React from 'react';
import { useNavigate } from 'react-router-dom';
import { Building2, ChevronLeft, CheckCircle2 } from 'lucide-react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { Button } from '@/shared/components/ui/button';
import { useCompanyStore, CompanySummary, TenantSummary } from '@/core/company/company.store';
import { toast } from '@/core/notifications/toast';

const mockTenants: TenantSummary[] = [
  {
    id: 't-1',
    name: 'هلدینگ سرمایه‌گذاری پیشگامان',
    companies: [
      { id: 'c-101', name: 'شرکت تجارت الکترونیک آیریک (دفاتر باز)', fiscalYear: 1403 },
      { id: 'c-102', name: 'صنایع تولیدی بهین‌کالا (کارخانه مرکزی)', fiscalYear: 1403 },
    ],
  },
  {
    id: 't-2',
    name: 'گروه بازرگانی سپهر',
    companies: [
      { id: 'c-201', name: 'بازرگانی بین‌المللی سپهر', fiscalYear: 1403 },
    ],
  },
];

export const CompanySelectionPage: React.FC = () => {
  const navigate = useNavigate();
  const { activeCompany, setActiveTenant, setActiveCompany } = useCompanyStore();

  const handleSelect = (tenant: TenantSummary, company: CompanySummary) => {
    setActiveTenant(tenant);
    setActiveCompany(company);
    toast.success('شرکت فعال تغییر یافت', `اکنون در حال مدیریت دفاتر «${company.name}» هستید.`);
    navigate('/');
  };

  return (
    <div className="max-w-2xl mx-auto space-y-6 py-8">
      <div className="text-center space-y-2">
        <h1 className="text-2xl font-bold text-foreground">انتخاب شرکت و دوره کاری</h1>
        <p className="text-sm text-muted-foreground">
          لطفا برای ادامه فعالیت و دسترسی به اطلاعات مالی، شرکت موردنظر خود را برگزینید.
        </p>
      </div>

      <div className="space-y-6">
        {mockTenants.map((tenant) => (
          <Card key={tenant.id} className="border-border shadow-xs">
            <CardHeader className="pb-3 border-b border-border/50 bg-muted/20">
              <div className="flex items-center gap-2">
                <Building2 className="w-5 h-5 text-primary" />
                <CardTitle className="text-base">{tenant.name}</CardTitle>
              </div>
              <CardDescription className="text-xs">
                شناسه مستأجر: <span className="font-mono">{tenant.id}</span>
              </CardDescription>
            </CardHeader>

            <CardContent className="pt-4 divide-y divide-border/60">
              {tenant.companies.map((company) => {
                const isSelected = activeCompany?.id === company.id;
                return (
                  <div
                    key={company.id}
                    className="flex items-center justify-between py-3.5 hover:bg-muted/40 px-3 rounded-md transition-colors"
                  >
                    <div className="flex flex-col">
                      <div className="flex items-center gap-2">
                        <span className="font-medium text-sm text-foreground">{company.name}</span>
                        {isSelected && (
                          <span className="flex items-center gap-1 text-[11px] text-success font-semibold">
                            <CheckCircle2 className="w-3.5 h-3.5" />
                            فعال
                          </span>
                        )}
                      </div>
                      <span className="text-xs text-muted-foreground">سال مالی: {company.fiscalYear}</span>
                    </div>

                    <Button
                      size="sm"
                      variant={isSelected ? 'default' : 'outline'}
                      onClick={() => handleSelect(tenant, company)}
                      className="cursor-pointer"
                    >
                      ورود به دفاتر
                      <ChevronLeft className="w-4 h-4 ms-1" />
                    </Button>
                  </div>
                );
              })}
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
};
