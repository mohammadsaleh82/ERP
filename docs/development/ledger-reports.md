# دفاتر، گردش حساب و ابعاد مالی (فاز ۷)

## ۱. قراردادهای واقعی Backend

| گزارش | Endpoint | Permission | صفحه |
| --- | --- | --- | --- |
| دفتر روزنامه | `GET /accounting/ledger/journal` (`fiscalYearId/fromDate/toDate/documentNumber/accountId/pageNumber/pageSize`) → JournalReportDto با Entries+Lines و DailyTotals | `ledger:read` | `/accounting/reports/journal` |
| دفتر کل | `GET /accounting/ledger/general-ledger` → GeneralLedgerReportDto (paged واقعی روی حساب‌ها، جمع دوره سرور) | `ledger:read` | `/accounting/reports/general-ledger` |
| گردش تفصیلی حساب | `GET /accounting/ledger/general-ledger/accounts/{id}/transactions` → سطر به سطر + مانده پایان | `ledger:read` | `/accounting/reports/general-ledger/:id` |
| دفتر معین | `GET /accounting/ledger/subsidiary-ledger?parentAccountId` → حساب‌های زیرمجموعه + جمع کل سرور | `ledger:read` | `/accounting/reports/subsidiary` |
| مانده حساب | `GET /accounting/ledger/balances` → AccountBalanceResultDto[] با جهت و `IsAbnormalBalance` | `ledger:read` | `/accounting/reports/balances` |
| کارت حساب | `GET /accounting/reports/account-card/{id}` → Opening/Side + Rows با RunningBalance + Closing/Side (paged) | `reports:read` | `/accounting/reports/account-card` |
| گردش حساب (تجمیعی) | `GET /accounting/reports/general-ledger` → GeneralLedgerRowDto[] (اول بدهکار/بستانکار، گردش، پایان) | `reports:read` | `/accounting/reports/turnover` |
| تراز آزمایشی | `GET /accounting/reports/trial-balance?level` → IsBalanced/TotalDebit/TotalCredit/Difference | `reports:read` | `/accounting/reports/trial-balance` |
| گردش ابعاد | `GET /accounting/dimensions/analysis/turnover?dimensionType&dimensionId` → DimensionTurnoverRowDto[] (paged) | `dimensions:read` | `/accounting/reports/dimensions?tab=` |
| ابعاد ترکیبی | `GET /accounting/dimensions/analysis/combined?dimensionTypes=2&dimensionTypes=3&maxRows` → هشدار `IsTruncated` | `dimensions:read` | `/accounting/reports/dimensions/combined` |

نکات binding: ledger از `fromDate/toDate` و reports از `from/to` استفاده می‌کند (دقیقاً مطابق signature کنترلرها پیاده شده). `dimensionTypes` به‌صورت **کلید تکرارشونده** در query string ارسال می‌شود (binding واقعی ASP.NET؛ بدون `[]`). سطح تراز آزمایشی با پارامتر `level`.

## ۲. فیلترهای وابسته هوشمند (ReportFilterBar)

- **سال → دوره**: دوره فقط پس از انتخاب سال فعال می‌شود؛ تغییر سال، دوره و بازه تاریخ نامعتبر را پاک می‌کند.
- **دوره → تاریخ**: انتخاب دوره واقعی، از/تا را به مرزهای همان دوره تنظیم می‌کند.
- **تاریخ دستی → دوره**: تغییر دستی تاریخ، دوره انتخابی را پاک می‌کند (تعارض سیگنال ممنوع).
- ابعاد (تفصیلی شناور/مرکز هزینه/پروژه/شخص) از `GET /accounting/dimensions` با `dimensionType` واقعی (۱/۲/۳/۶) بارگذاری می‌شوند — **هیچ Entity محلی بازسازی نشده**؛ اشخاص متعلق به ماژول Parties صرفاً از قرارداد ابعاد نمایش داده می‌شوند.
- همه فیلترها در querystring صفحه (قابل اشتراک‌گذاری)؛ هر تغییر فیلتر به صفحه ۱ برمی‌گردد.

## ۳. جدول مالی مشترک (financial-table.tsx)

MoneyCell (هزارگان تبلار؛ بدهکار آبی/بستانکار کهربایی؛ صفر → —) · BalanceCell (بج جهت بدهکار/بستانکار + آیکن هشدار مانده خلاف ماهیت) · DocNumberCell (لینک سند مرجع + دکمه کپی با toast) · DescriptionCell (Tooltip برای شرح طولانی) · Loading skeleton / EmptyState · `overflow-x-auto` برای نمایشگر کوچک · footer جمع‌ها.

## ۴. دقت مالی و Cache

- مبالغ از سرور به رشته decimal و فرمت نمایش با decimal.js (`formatAmount`) — بدون float.
- **companyId در همه query key ها** + `queryClient.clear()` در تغییر شرکت (قرارداد header switcher) → cache شرکت قبلی هرگز بازمصرف نمی‌شود (تست دارد).

## ۵. تست‌ها (۱۸ مورد فاز ۷ — مجموع ۱۹۵ سبز)

لینک و کپی سند · جمع دوره سرور · صفحه‌بندی/فیلترهای سرور با نگه‌داری URL · drill-down دفتر کل با حفظ فیلترها · سه سناریو فیلتر وابسته (سال→پاک‌سازی وابسته‌ها، دوره→مرز تاریخ، تاریخ→پاک‌سازی دوره) · کارت حساب (مانده اول/تراکمی/پایان + فیلترهای ابعاد) · هشدار مانده غیرعادی · بنر توازن تراز آزمایشی + سطح تجمیع · تب‌های ابعاد با type واقعی و ریست انتخاب · combined با کلید تکرارشونده + سقف ۳ بعد + IsTruncated · تغییر شرکت/cache.

## ۶. Gaps / محدودیت‌ها

1. **خروجی Excel/PDF گزارش‌ها**: endpoint export در backend برای این گزارش‌ها وجود ندارد؛ فقط چاپ مرورگر (Ctrl+P) — پیاده‌سازی نشده تا قرارداد واقعی باشد.
2. **LedgerQueryRequest فیلتر ابعاد ندارد**: journal/general-ledger در LedgerController costCenterId/projectId/partyId نمی‌گیرند (فقط ReportsController ReportFilter دارد) → فیلتر ابعاد روی کارت حساب/گردش حساب/تراز فعال است و روی روزنامه/دفتر کل ledger نه (مستند به قرارداد).
3. **مرتب‌سازی ستونی سمت سرور**: PagedRequest.sortBy در endpointهای ledger دفتر کل/روزنامه مصرف می‌شود ولی محدودیت مرتب‌سازی مجاز backend در service نامشخص است؛ مرتب‌سازی UI روی ستون‌های شماره/تاریخ فقط در لیست اسناد (فاز ۶) فعال است — برای دفاتر، ترتیب backend (تاریخ/شماره سند) حفظ می‌شود.
4. گزارش‌های statement (ترازنامه/سود و زیان/جریان وجه نقد/receivables/payables/aging) در backend موجودند ولی خارج از دامنه این فاز بودند (فاز گزارش‌های مالی).
