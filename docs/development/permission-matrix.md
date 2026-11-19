# ماتریس مجوزهای حسابداری (PROMPT 09)

منبع: `src/ERP.BuildingBlocks/Security/AccountingPermissions.cs`. هر مجوز یک Policy مستقل در ASP.NET Core است (نام مجوز = نام سیاست، `PermissionAuthorizationHandler`). مجوزها روی **Tenant + Company** (هدرهای زمینه) و در صورت کاربرد **Branch** (ادعای `ActiveBranchId`) اعمال می‌شوند.

| # | عملیات | ثابت | رشته مجوز |
| --- | --- | --- | --- |
| ۱ | مشاهده حساب‌ها | `ViewAccounts` | `accounting:accounts:read` |
| ۲ | ایجاد حساب | `CreateAccounts` | `accounting:accounts:create` |
| ۳ | تغییر حساب | `UpdateAccounts` | `accounting:accounts:update` |
| ۴ | غیرفعال‌سازی حساب | `DeactivateAccounts` | `accounting:accounts:deactivate` |
| ۵ | مشاهده اسناد | `ViewVouchers` | `accounting:documents:read` |
| ۶ | ایجاد سند | `CreateVouchers` | `accounting:documents:create` |
| ۷ | ویرایش پیش‌نویس | `UpdateDraftVouchers` | `accounting:documents:update` |
| ۸ | حذف پیش‌نویس | `DeleteDraftVouchers` | `accounting:documents:delete` |
| ۹ | ارسال برای تأیید | `SubmitVouchers` | `accounting:documents:submit` |
| ۱۰ | تأیید سند | `ApproveVouchers` | `accounting:documents:approve` |
| ۱۱ | رد سند | `RejectVouchers` | `accounting:documents:reject` |
| ۱۲ | قطعی‌سازی سند | `PostVouchers` | `accounting:documents:post` |
| ۱۳ | معکوس‌سازی سند | `ReverseVouchers` | `accounting:documents:reverse` |
| ۱۴ | مشاهده دفتر | `ViewLedger` | `accounting:ledger:read` |
| ۱۵ | مشاهده گزارش‌های مالی | `ViewFinancialReports` | `accounting:reports:read` |
| ۱۶ | ایجاد سال مالی | `CreateFiscalYear` | `accounting:fiscal-years:create` |
| ۱۷ | بستن دوره مالی | `CloseFiscalPeriod` | `accounting:periods:close` |
| ۱۸ | بستن سال مالی | `CloseFiscalYear` | `accounting:fiscalyear:close` |
| ۱۹ | بازگشایی سال مالی | `ReopenFiscalYear` | `accounting:fiscalyear:reopen` |
| ۲۰ | مدیریت تنظیمات حسابداری | `ManageAccountingSettings` | `accounting:settings:update` |

## پوشش روی Controllerها

تست `PermissionMatrixTests` تضمین می‌کند: (۱) همه ۲۰ مجوز یکتا هستند، (۲) هر مجوز روی حداقل یک Action اعمال شده است. مجوزهای کمکی خارج از ماتریس (seed، ابعاد، افتتاحیه/تعدیل، بازسازی دفتر، طبقه‌بندی گزارش‌ها، خواندن تنظیمات) حفظ شده‌اند؛ از جمله `accounting:audit:read` برای خواندن دفتر ممیزی عمومی.

## نکات اعمال مجوز

- همه endpointهای گزارش/خروجی: `ViewFinancialReports`؛ دفتر کل و گردش‌ها: `ViewLedger`؛ بازگشایی سال: `ReopenFiscalYear` مستقل از بستن سال.
- غیرفعال‌سازی حساب (`POST /accounts/{id}/deactivate`) دیگر از مجوز Update استفاده نمی‌کند و مجوز مستقل دارد.
- بستن دوره (`POST /fiscal-years/{id}/periods/{pid}/close`) از `CloseFiscalPeriod` استفاده می‌کند که با `CloseFiscalYear` متفاوت است.
