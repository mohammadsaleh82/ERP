# معماری دوره‌های مالی، تقویم و تنظیمات حسابداری (Fiscal Year, Accounting Periods & Settings)

## ۱. مقدمه و دامنه تجاری

مدیریت دوره‌های مالی و تنظیمات حسابداری، زیربنای ثبت قانونی، زمانی و کنترلی اسناد مالی در یک ERP سازمانی چندشرکتی است. این زیرسیستم امکان مدیریت سال‌های مالی، بخش‌بندی سال به دوره‌های زمانی، پشتیبانی همزمان از تقویم جلالی و میلادی، مدیریت شماره‌گذاری ایمن اسناد و پایش تاریخچه تنظیمات مالی را با ایزولاسیون کامل شرکت‌ها فراهم می‌آورد.

---

## ۲. موجودیت و قوانین سال مالی (`FiscalYear`)

موجودیت `FiscalYear` به‌عنوان یک **Aggregate Root**، مرز دوره فعالیت مالی هر شرکت را تعیین می‌کند.

### مشخصات کلیدی:
- `Id`: شناسه یکتا (GUID)
- `TenantId`, `CompanyId`: تفکیک چندمستاجره و چندشرکتی
- `YearNumber`: شماره سال (مثلاً ۱۴۰۳ یا ۲۰۲۴)
- `Name`: عنوان سال مالی (مثلاً "سال مالی ۱۴۰۳")
- `StartDate`, `EndDate`: مرزهای زمانی بر پایه نوع `DateOnly`
- `CalendarType`: تقویم عملیاتی (`Persian = 1`, `Gregorian = 2`)
- `Status`: وضعیت سال مالی (`Draft = 1`, `Open = 2`, `Closing = 3`, `Closed = 4`)
- `ClosedAtUtc`, `ClosedBy`: اطلاعات ثبت نهایی اختتامیه
- `Version`: توکن کنترل همزمانی خوش‌بینانه (`Optimistic Concurrency Token`)

### قوانین اکید سال مالی (Invariants):
1. **ترتیب زمانی معتبر**: تاریخ پایان باید اکیداً بعد از تاریخ شروع باشد (`EndDate > StartDate`).
2. **پیشگیری از همپوشانی سال‌های مالی**: در یک شرکت، هیچ دو سال مالی نباید همپوشانی تاریخی داشته باشند:
   $$\text{Existing.StartDate} \le \text{New.EndDate} \quad \land \quad \text{New.StartDate} \le \text{Existing.EndDate}$$
3. **تغییرناپذیری سال بسته شده (`Immutability`)**: سال مالی که به وضعیت `Closed` درآمده، غیرقابل تغییر بوده و اجازه افزودن یا ویرایش دوره‌ها را نمی‌دهد.
4. **استقلال شرکت‌ها**: ایجاد سال مالی در شرکت A هیچ اثری بر سال‌های مالی شرکت B ندارد.

---

## ۳. دوره‌های مالی (`FiscalPeriod`)

هر سال مالی به چند دوره حسابداری تقسیم می‌شود. سیستم از مدل‌های زیر پشتیبانی می‌کند:
- **ماهانه (`Monthly`)**: ۱۲ دوره استاندارد
- **فصلی (`Quarterly`)**: ۴ دوره استاندارد (بهار، تابستان، پاییز، زمستان)
- **سفارشی (`Custom`)**: دوره‌های دلخواه یا دوره تعدیلات و اختتامیه (`IsAdjustmentPeriod`)

### قوانین اکید دوره‌ها:
1. **قرارگیری کامل درون سال مالی**: تاریخ شروع و پایان دوره باید کاملاً در بازه `[FiscalYear.StartDate, FiscalYear.EndDate]` قرار گیرد.
2. **پیشگیری از همپوشانی دوره‌ها**: دوره‌های عادی نباید با یکدیگر تداخل تاریخی داشته باشند.
3. **پذیرش سند صرفاً در دوره باز**: متد `EnsureCanAcceptTransactions` در صورتی که وضعیت دوره غیر از `Open` باشد (مانند `Locked` یا `Closed`)، مانع ثبت سند شده و `FiscalPeriodClosedException` صادر می‌کند.

---

## ۴. معماری تقویم مالی (`FinancialCalendarService`)

برای پرهیز از باگ‌های محاسباتی و تبدیل رشته‌ای، کلیه مقایسه‌ها و ذخیره‌سازی‌ها بر مبنای `DateOnly` انجام می‌شود:

- **تقویم هجری شمسی (جلالی)**:
  - استفاده از الگوریتم استاندارد `System.Globalization.PersianCalendar`.
  - ۶ ماه اول سال ۳۱ روزه.
  - ۵ ماه دوم سال ۳۰ روزه.
  - ماه ۱۲ (اسفند) در سال‌های عادی ۲۹ روزه و در سال‌های کبیسه ۳۰ روزه (محاسبه خودکار کبیسه حسابی/نجومی جلالی).
- **تقویم میلادی**:
  - ماه‌های استاندارد گریگوری با احتساب کبیسه فوریه ۲۸/۲۹ روزه.
- **تبدیل مرزهای سال**:
  - متدهای `ToPersianDate`، `ToGregorianDate` و `FormatDate` بدون دستکاری رشته‌ای، مقادیر دقیق را نگاشت می‌کنند (مانند نگاشت ۱ فروردین ۱۴۰۳ به `2024-03-20`).

---

## ۵. تنظیمات حسابداری و تاریخچه تغییرات (`AccountingSettings & History`)

تنظیمات عملیاتی هر شرکت در `AccountingSettings` نگهداری می‌شود:
- `DefaultCurrencyCode`: واحد پول پیش‌فرض (مانند `IRR`, `USD`, `EUR`)
- `CalendarType`: تقویم انتخابی شرکت
- `FiscalYearStartMonth`, `FiscalYearStartDay`: مبدأ آغاز سال مالی
- `DecimalPrecision`: دقت ارقام اعشار (۰ برای ریال/تومان، ۲ برای ارزهای خارجی)
- `RoundingMode`: رویه گرد کردن (`RoundToEven`, `AwayFromZero`, `Floor`, `Ceiling`)
- `VoucherNumberingPolicy`: سیاست شماره‌گذاری سند (`ContinuousByFiscalYear`, `MonthlyRestart`, `DailyRestart`)
- `AllowNegativeBalancesPolicy`: کنترل مانده منفی غیرعادی (`StrictlyForbidden`, `WarningOnly`, `Allowed`)
- `ApprovalPolicy`: فرآیند تایید و گردش سند (`DirectPostingAllowed`, `TwoStepApprovalRequired`, `ThreeStepApprovalRequired`)
- `LockDate`: تاریخ قفل اسناد (عدم امکان ثبت سند در تاریخ‌های قبل از این تاریخ)

### اصل تغییرناپذیری عطف‌به‌ماسبق:
ویرایش تنظیمات حسابداری شرکت، رکوردهای قبلی اسناد مالی ثبت‌شده یا سال‌های بسته شده را تغییر **نمی‌دهد**. با هر بار ویرایش، یک کپی تغییرناپذیر در جدول `acc_accounting_settings_history` همراه با `Version`، نام تغییردهنده (`ChangedBy`)، زمان (`ChangedAtUtc`) و علت تغییر (`ChangeReason`) ثبت می‌گردد.

---

## ۶. زیرساخت شماره‌گذاری امن و ضد تصادم اسناد (`VoucherNumberingService`)

برای صدور شماره سندهای ترتیبی و گپ‌ناپذیر:
1. **عدم استفاده از `MAX(Number)+1`**: زیرا تحت بار همزمان و تراکنش‌های موازی منجر به شماره تکراری یا قفل‌های سنگین جدولی می‌شود.
2. **جدول ترکر سریال (`acc_voucher_sequences`)**: یک رکورد اختصاصی برای هر ترکیب `(TenantId, CompanyId, FiscalYearId, SequenceType, PeriodId)` نگهداری می‌شود.
3. **مکانیزم همروندی امن**:
   - بهره‌گیری از توکن `Version` و حلقه سعی مجدد خوش‌بینانه (Optimistic Retry Loop with Exponential Jitter).
   - در تست‌های همزمان با ۲۰ ترد موازی، تولید اعداد دقیقاً ۱ تا ۲۰ بدون کوچک‌ترین تکرار تضمین شده است.
4. **قالب‌بندی پویا (`FormatPattern`)**: پشتیبانی از پیش‌شماره و الگوهای منعطف نظیر `{Prefix}{YearNumber}-{Seq:D6}` یا شماره سریال خالص عددی.
5. **تفکیک اسناد پیش‌نویس از قطعی**: شماره‌گذاری پیش‌نویس (`Draft`) و شماره قطعی دائم (`Posted`) در توالی‌های مستقل مدیریت می‌شوند.

---

## ۷. ماتریس دسترسی‌ها و Endpointهای API

کلیه عملیات از طریق کنترلرهای `FiscalYearsController` و `AccountingSettingsController` در مسیر `/api/v1/accounting/` ارائه می‌شوند:

| متد | مسیر (Route) | دسترسی الزامی (`HasPermission`) | شرح عملیات |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/v1/accounting/fiscal-years` | `accounting:fiscal-years:create` | ایجاد سال مالی جدید |
| `GET` | `/api/v1/accounting/fiscal-years` | `accounting:fiscal-years:read` | لیست سال‌های مالی شرکت |
| `GET` | `/api/v1/accounting/fiscal-years/{id}` | `accounting:fiscal-years:read` | جزئیات سال مالی و دوره‌ها |
| `POST` | `/api/v1/accounting/fiscal-years/{id}/open` | `accounting:fiscal-years:update` | بازگشایی سال مالی |
| `POST` | `/api/v1/accounting/fiscal-years/{id}/close` | `accounting:fiscal-years:update` | بستن سال مالی |
| `POST` | `/api/v1/accounting/fiscal-years/{id}/periods/generate` | `accounting:fiscal-years:update` | تولید خودکار دوره‌ها (ماهانه/فصلی) |
| `POST` | `/api/v1/accounting/fiscal-years/{id}/periods/custom` | `accounting:fiscal-years:update` | ایجاد دوره مالی سفارشی |
| `GET` | `/api/v1/accounting/fiscal-years/{id}/periods` | `accounting:fiscal-years:read` | لیست دوره‌های یک سال مالی |
| `POST` | `/api/v1/accounting/fiscal-years/{id}/periods/{pId}/open` | `accounting:fiscal-years:update` | بازگشایی دوره مالی |
| `POST` | `/api/v1/accounting/fiscal-years/{id}/periods/{pId}/close` | `accounting:fiscal-years:update` | بستن قطعی دوره مالی |
| `GET` | `/api/v1/accounting/settings` | `accounting:settings:read` | دریافت تنظیمات جاری شرکت |
| `PUT` | `/api/v1/accounting/settings` | `accounting:settings:update` | به‌روزرسانی تنظیمات و ثبت تاریخچه |
| `GET` | `/api/v1/accounting/settings/history` | `accounting:settings:read` | دریافت تاریخچه تغییرات تنظیمات |

---

## ۸. خلاصه نتایج آزمون‌های تضمین کیفیت (QA Summary)

تست‌های نوشته شده در [FiscalYearAndSettingsTests.cs](file:///d:/ffff/MyProject/ERP/tests/ERP.UnitTests/FiscalYearAndSettingsTests.cs) کلیه ۱۱ سناریوی تصریح‌شده در پرامپت را پوشش می‌دهند:

```text
Passed!  - Failed: 0, Passed: 50, Skipped: 0 - ERP.UnitTests.dll (net10.0)
Passed!  - Failed: 0, Passed: 17, Skipped: 0 - ERP.ArchitectureTests.dll (net10.0)
Passed!  - Failed: 0, Passed:  6, Skipped: 0 - ERP.IntegrationTests.dll (net10.0)
Passed!  - Failed: 0, Passed: 15, Skipped: 0 - ERP.SecurityTests.dll (net10.0)
-----------------------------------------------------------------------------------
مجموع: ۸۸ تست خودکار با موفقیت ۱۰۰٪ پاس شدند.
Frontend: بیلد و تست Lint بدون هیچ خطا و اخطاری تایید گردید.
```
