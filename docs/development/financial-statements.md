# گزارش‌گیری مالی و صورت‌های مالی (فاز ۸)

## ۱. قراردادهای واقعی Backend (ReportsController — همه محاسبات سمت سرور)

| گزارش | Endpoint | صفحه | خروجی export |
| --- | --- | --- | --- |
| ترازنامه | `GET balance-sheet` (`fiscalYearId/fiscalPeriodId/to/costCenterId/projectId/currencyCode`) → BalanceSheetDto با IsBalanced/Difference | `/accounting/reports/balance-sheet` | balancesheet |
| سود و زیان | `GET profit-loss` (`from/to/...`) → سودهای مرحله‌ای + بخش‌ها | `/accounting/reports/profit-loss` | profitloss |
| جریان وجوه نقد | `GET cash-flow` — روش مستقیم، فقط `CashFlowCategory` طبقه‌بندی‌شده | `/accounting/reports/cash-flow` | cashflow |
| بدهکاران | `GET receivables` → PartyLedgerResultDto (accountsConfigured/note) | `/accounting/reports/receivables` | receivables |
| بستانکاران | `GET payables` | `/accounting/reports/payables` | payables |
| گردش اشخاص | `GET party-turnover` (از فاز ۷ در ابعاد هم پوشش دارد) | — | partyturnover |
| گزارش سنی | `GET aging?to` → سبد‌های سنی + hasReliableDueDates/note | `/accounting/reports/aging` | **ندارد — فقط چاپ** |
| طبقه‌بندی جریان نقد | `GET/PUT cash-flow-classifications` (perm `reports:classify`) | بخش مدیریت در صفحه cash-flow | — |

فاز ۷ (این پروژه): تراز آزمایشی/روزنامه/دفتر کل/معین/مانده/کارت حساب/گردش/ابعاد — صفحه‌های آن فاز با Shell فاز ۸ ارتقا یافتند (روی ۴ صفحه exportable).

## ۲. خروجی‌ها — POST /accounting/reports/export (سمت سرور)

- بدنه: `{ReportName, Format(Excel|Pdf|Csv), FiscalYearId, FiscalPeriodId, From, To, AccountId, AccountLevel, PartyId, CostCenterId, ProjectId, CurrencyCode}` — منبع اعداد همان سرویس‌های گزارش JSON است («تطابق کامل» طبق comment کنترلر).
- reportNames پذیرفته‌شده: `trialbalance, journal(۱-۵۰۰ ردیف), generalledger, balancesheet, profitloss, cashflow, receivables, payables, partyturnover, accountcard(نیاز به accountId)`.
- خروجی با **همان فیلترهای انتخابی** صفحه ارسال می‌شود؛ فیلتری که endpoint خروجی پشتیبانی نمی‌کند (مثل detailId کارت حساب) آگاهانه از exportParams حذف شده تا ادعای «منطبق با فیلترها» صادق بماند.
- برای فایل‌های بزرگ: محاسبه و تولید فایل سمت سرور است؛ مرورگر فقط blob را ذخیره می‌کند.
- چاپ: قالب `@media print` (حذف کنترل‌ها، A4، جدول کامل، RTL) — در index print همه‌ی صفحات گزارش.

## ۳. صحت مالی (کنترل صحت)

- Frontend هیچ محاسبه نهایی صورت‌های مالی انجام نمی‌دهد: بخش‌ها/سودهای مرحله‌ای/جمع‌ها/اختلاف توازن همگی از DTO خوانده می‌شوند؛ جمع‌های جمع‌بندی client (cf-totals، سهم سنی) فقط جمعِ نمایشیِ مقادیر API هستند.
- عدم توازن ترازنامه، `cashAccountsConfigured=false`، `accountsConfigured=false`، `hasReliableDueDates=false` همگی با بنر هشدار «گزارش قطعی نیست» همراه می‌شوند.
- جریان وجوه نقد **صرفاً از طبقه‌بندی backend**؛ استنتاج از مانده حساب‌های بانکی وجود ندارد (بنر صریح + متن صفحه).
- زمان تولید گزارش از `dataUpdatedAt` react-query با فرت fa-IR.

## ۴. تجربه گزارش‌گیری (ReportPageShell مشترک)

عنوان/توضیح · نشان شرکت فعال (تعویض از هدر جهانی — `queryClient.clear()` ضد cache شرکت قبلی) · فیلترهای وابسته فاز ۷ · دکمه «اجرای گزارش» · Excel/PDF/CSV (فقط گزارش‌های exportable) · چاپ · زمان تولید · خلاصه (summary chips) · جدول اصلی + جمع نهایی · EmptyState · ErrorState با retry · query صفحات جدید فقط پس از «اجرا» فعال می‌شود؛ صفحات ارتقایافته فاز ۷ auto-run با refetch دکمه اجرا.

## ۵. نمودارها (recharts — فقط مقادیر API)

سود و زیان: ستونی مراحل سود · جریان نقد: ورود/خروج/خالص به تفکیک طبقه · سنی: مبلغ هر سبد. نمودارها `report-no-print` هستند و جایگزین جدول دقیق نمی‌شوند.

## ۶. تست‌ها (۱۷ مورد جدید — مجموع ۲۱۲ سبز)

اجرای روی تقاضا + ارسال دقیق فیلترها (to/from/fiscalYearId) · تغییر سال مالی با پاک‌سازی وابسته‌ها · بدون داده · خروجی اکسل با تطبیق فیلترها + مسیر خطای خروجی · چاپ (window.print) · عدد بزرگ `1,234,567,890,123` · تطبیق جمع‌ها با داده API (ترازنامه/سود و زیان/نقد/بدهکاران/سنی) · خطای API با retry · دسترسی مجاز/غیرمجاز (هدایت به access-denied) · بنرهای غیرقطعی (عدم توازن/طبقه‌بندی‌نشده/سررسید غیرقابل‌اتکا) · مدیریت طبقه‌بندی جریان نقد با enum واقعی.

## ۷. Gaps / محدودیت‌ها

1. `aging` و گزارش‌های ابعادی/معین در endpoint خروجی backend نیستند → فقط چاپ (بدون دکمه اکسل/PDF/CSV).
2. export دفتر روزنامه سمت سرور فقط ۵۰۰ ردیف اول را می‌گیرد (مطابق فراخوانی `GetJournalAsync(filter, 1, 500)` در کنترلر) — برای بازه بزرگ‌تر باید بازه را کوتاه‌تر کرد؛ محدودیت در description صفحه ذکر نشده چون پیام toast نام فایل می‌دهد.
3. «انتخاب شرکت» درون‌صفحه‌ای: تعویض شرکت جلسه را refresh می‌کند و کل cache پاک می‌شود؛ بنابراین انتخابگر شرکت در Shell فقط نمایشی است (نشان شرکت فعال) و تعویض از هدر جهانی انجام می‌شود.
4. طبقه‌بندی ریشه‌های کدینگ (PUT classifications) UI جداگانه ندارد — از قبل در backend موجود؛ در صورت نیاز فاز بعد صفحه تنظیمات طبقه‌بندی اضافه می‌شود.
5. وابستگی نمودارها به recharts (از قبل نصب) — bundle-size معادل‌های سبک‌تر بررسی نشد.
