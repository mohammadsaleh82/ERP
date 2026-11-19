# معماری داشبورد و UX عمومی (فاز ۴)

## ۱. منابع داده — فقط APIهای واقعی (بدون عدد ساختگی)

| Widget | Endpoint واقعی | Permission لازم |
| --- | --- | --- |
| کارت‌های گردش بدهکار/بستانکار | `GET /Ledger/balances` (جمع `periodDebit/periodCredit`) | `accounting:ledger:read` |
| اسناد بازه جاری | `GET /Documents?fromDate&toDate` → `totalCount` | `accounting:documents:read` |
| اسناد در انتظار تأیید | `GET /Documents?status=PendingApproval` | `accounting:documents:read` |
| روند ثبت اسناد (نمودار) | از `DocumentDate` اسناد همان پاسخ (۱۴ روز اخیر بازه) | `accounting:documents:read` |
| وضعیت دوره مالی | `GET /FiscalYears` + `GET /FiscalYears/{id}/periods` | `accounting:fiscal-years:read` |
| بزرگ‌ترین مانده حساب‌ها | `GET /Ledger/balances` (Top بر `closingDisplayBalance`) | `accounting:ledger:read` |
| فعالیت‌های اخیر | `GET /AuditTrail?page=1&pageSize=8` | `accounting:audit:read` |
| عملیات پرتکرار (میانبرها) | ناوبری فقط با مجوز واقعی هر عملیات | — |
| هشدارهای مالی | — (بدون API) → Empty + برچسب «نیاز Backend» | — |
| درآمد و هزینه | — (بدون endpoint تجمیع ماهیت حساب) → نیاز Backend؛ مستند gap | — |

همه قراردادها از DTOهای واقعی: `AccountBalanceResultDto`, `DocumentDto/PagedDocumentsResult`, `FiscalYearDto/FiscalPeriodDto`, `AuditTrailEntryDto`, `PagedResult`.

## ۲. معماری Widget مستقل

`shared/components/widget/widget-shell.tsx` — هر widget با ۵ حالت مستقل:
- **Loading**: Skeleton + `aria-busy`
- **Success**: داده واقعی
- **Empty**: پیام زمینه‌ای + برچسب اختیاری «نیاز Backend»
- **Error**: پیام + جزئیات + دکمه «تلاش مجدد» (refetch فقط همان query)
- **Unauthorized**: permission ماتریس — query هرگز `enabled` نمی‌شود و درخواست ارسال نمی‌شود

خطای یک widget کل داشبورد را نمی‌اندازد (error state فقط در shell همان widget؛ تست: balances موفق + documents خطا → کارت‌های موفق همچنان داده می‌کنند).

## ۳. Query scoping و تعویض شرکت

کلیدهای query همیشه با `companyId` فعال scope می‌شوند: `['dashboard', companyId, 'balances', from, to]`. تعویض شرکت (فاز ۳) با `queryClient.clear()` هیچ داده شرکت قبلی باقی نمی‌گذارد.

## ۴. فیلتر بازه زمانی و بازگشت state

`?from=&to=` در URL (`replace: true` بدون پرش اسکرول) — همه queryهای وابسته از همین بازه. تست URL-persist: مقدار اولیه از URL خوانده می‌شود، تغییر input → query string و refetch با `from` جدید.

## ۵. جست‌وجوی سراسری (Header)

- **Debounce ۳۰۰ms** + حداقل ۲ کاراکتر
- **اسناد**: فقط عبارت عددی → `GET /Documents?documentNumber=` (قرارداد واقعی `SearchDocumentsRequest`)
- **حساب‌ها**: `GET /Accounts?searchTerm=` (کد/نام — قرارداد واقعی `AccountFilterRequest`)
- **اشخاص/گزارش‌ها**: بدون API جست‌وجو → بخش Empty با برچسب «نیاز Backend»
- وضعیت مستقل هر بخش: Loading/Empty/Error/Success + «نتیجه‌ای یافت نشد» سراسری

## ۶. UX روزانه

- خوشامدگویی شخصی‌سازی‌شده (نام واقعی + روز/شب) + شرکت/سال مالی فعال واقعی
- Badge سال مالی در Header از `activeFiscalYear` companies API (فاز ۳)
- نتیجه‌ها با ارقام فارسی و تاریخ شمسی (`formatNumberPersian/formatToPersianDate`)
- تعداد اسناد روند نموداری: فقط تا PageSize=100 سند اخیر بازه (محدودیت مستند؛ صفحه‌بندی کامل در فهرست اسناد)

## ۷. تست‌ها (۱۶ جدید؛ مجموع ۱۱۶ سبز)

جمع واقعی کارت‌ها از balances · شمارش از totalCount · unauthorized بدون ارسال درخواست · خطای مستقل widget (سایرها سالم) · PendingApprovals داده/Empty · TopBalances حذف مانده صفر · FiscalStatus باز/بسته · هشدار بدون داده ساختگی + نیاز Backend · میانبرها با مجوز · debounce دقیقا یک درخواست · سند فقط برای عبارت عددی · Empty بخش اشخاص/گزارش‌ها · خطای مستقل سرچ · URL persist فیلتر بازه.

## ۸. Gaps مستندشده (نیاز Backend)

1. **درآمد/هزینه**: endpoint تجمیع بر `AccountNature` ندارد (پیشنهاد: `GET /Reports/income-statement-summary`)
2. **هشدارهای مالی**: endpoint هشدار ندارد
3. **جست‌وجوی سراسری اشخاص/گزارش‌ها**: API جست‌وجوی unified ندارد
4. **دوره مالی فعال**: از `FiscalPeriodDto.status` باز موجود است و استفاده شد
