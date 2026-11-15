import React from 'react';
import {
  TrendingUp,
  Receipt,
  Landmark,
  Boxes,
  ArrowUpRight,
  Plus,
} from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { Breadcrumb } from '@/shared/components/layout/breadcrumb';
import { DataTable, Column } from '@/shared/components/tables/data-table';
import { useCompanyStore } from '@/core/company/company.store';

interface RecentTransaction {
  id: string;
  voucherNo: string;
  date: string;
  description: string;
  amount: string;
  status: 'Posted' | 'Approved' | 'Draft';
}

const mockTransactions: RecentTransaction[] = [
  {
    id: '1',
    voucherNo: '۱۴۰۳/۰۰۱۸۴',
    date: '۱۴۰۳/۰۸/۲۴',
    description: 'فاکتور فروش شماره ۸۴۹۱ - شرکت پیشرو تجارت',
    amount: '۱۲۵,۴۰۰,۰۰۰ ریال',
    status: 'Posted',
  },
  {
    id: '2',
    voucherNo: '۱۴۰۳/۰۰۱۸۳',
    date: '۱۴۰۳/۰۸/۲۳',
    description: 'حواله خروج انبار مرکزی - بارنامه ۴۸۱',
    amount: '۴۵,۲۰۰,۰۰۰ ریال',
    status: 'Approved',
  },
  {
    id: '3',
    voucherNo: '۱۴۰۳/۰۰۱۸۲',
    date: '۱۴۰۳/۰۸/۲۲',
    description: 'دستور پرداخت چک صیادی بانک ملت به تامین‌کننده',
    amount: '۳۱۰,۰۰۰,۰۰۰ ریال',
    status: 'Posted',
  },
  {
    id: '4',
    voucherNo: '۱۴۰۳/۰۰۱۸۱',
    date: '۱۴۰۳/۰۸/۲۱',
    description: 'سند تسویه تنخواه‌گردان شعبه مرکزی',
    amount: '۱۸,۵۰۰,۰۰۰ ریال',
    status: 'Draft',
  },
];

const columns: Column<RecentTransaction>[] = [
  { key: 'voucherNo', header: 'شماره سند', width: '130px' },
  { key: 'date', header: 'تاریخ سند', width: '110px' },
  { key: 'description', header: 'شرح رویداد مالی' },
  { key: 'amount', header: 'مبلغ کل', isNumeric: true, align: 'end', width: '160px' },
  {
    key: 'status',
    header: 'وضعیت',
    width: '100px',
    align: 'center',
    render: (item) => {
      switch (item.status) {
        case 'Posted':
          return <Badge variant="success">ثبت قطعی</Badge>;
        case 'Approved':
          return <Badge variant="info">تاییدشده</Badge>;
        case 'Draft':
          return <Badge variant="warning">پیش‌نویس</Badge>;
      }
    },
  },
];

export const DashboardPage: React.FC = () => {
  const { activeCompany } = useCompanyStore();

  return (
    <div className="space-y-6">
      {/* Breadcrumb & Page Header */}
      <div>
        <Breadcrumb items={[{ label: 'داشبورد مدیریتی' }]} />
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 mt-2">
          <div>
            <h1 className="text-2xl font-bold text-foreground">
              مرکز کنترل و نظارت مالی {activeCompany ? `— ${activeCompany.name}` : ''}
            </h1>
            <p className="text-sm text-muted-foreground mt-1">
              خلاصه وضعیت تراز دفاتر، اسناد معلق و جریان وجوه نقد در سال مالی جاری
            </p>
          </div>

          <div className="flex items-center gap-2">
            <Button size="sm" className="gap-1.5 cursor-pointer">
              <Plus className="w-4 h-4" />
              صدور سند حسابداری جدید
            </Button>
          </div>
        </div>
      </div>

      {/* KPI Cards Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <Card className="shadow-xs">
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-xs font-medium text-muted-foreground">درآمد عملیاتی دوره</CardTitle>
            <TrendingUp className="w-4 h-4 text-success" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold font-tabular">۲,۴۸۰,۵۰۰,۰۰۰ <span className="text-xs font-normal text-muted-foreground">ریال</span></div>
            <p className="text-xs text-success flex items-center gap-1 mt-1 font-medium">
              <ArrowUpRight className="w-3.5 h-3.5" />
              +۱۲.۴٪ نسبت به ماه گذشته
            </p>
          </CardContent>
        </Card>

        <Card className="shadow-xs">
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-xs font-medium text-muted-foreground">تعداد اسناد صادره</CardTitle>
            <Receipt className="w-4 h-4 text-primary" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold font-tabular">۱۸۴ <span className="text-xs font-normal text-muted-foreground">فقره سند</span></div>
            <p className="text-xs text-muted-foreground mt-1">
              ۱۷۲ دائم | ۱۲ در انتظار بررسی
            </p>
          </CardContent>
        </Card>

        <Card className="shadow-xs">
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-xs font-medium text-muted-foreground">ارزش ریالی موجودی انبار</CardTitle>
            <Boxes className="w-4 h-4 text-warning" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold font-tabular">۸,۹۳۰,۰۰۰,۰۰۰ <span className="text-xs font-normal text-muted-foreground">ریال</span></div>
            <p className="text-xs text-muted-foreground mt-1">
              روش میانگین موزون متحرک
            </p>
          </CardContent>
        </Card>

        <Card className="shadow-xs">
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-xs font-medium text-muted-foreground">موجودی نقد و بانک</CardTitle>
            <Landmark className="w-4 h-4 text-info" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold font-tabular">۱,۱۲۵,۰۰۰,۰۰۰ <span className="text-xs font-normal text-muted-foreground">ریال</span></div>
            <p className="text-xs text-muted-foreground mt-1">
              ۴ حساب بانکی فعال
            </p>
          </CardContent>
        </Card>
      </div>

      {/* Recent Vouchers Section */}
      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="text-base font-bold text-foreground">آخرین رویدادها و اسناد حسابداری</h2>
          <Button variant="link" size="sm" className="text-xs">
            مشاهده دفتر روزنامه کامل
          </Button>
        </div>

        <DataTable
          columns={columns}
          data={mockTransactions}
          emptyTitle="هیچ سندی ثبت نشده است"
        />
      </div>
    </div>
  );
};
