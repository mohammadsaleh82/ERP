# کدینگ حساب‌ها و پیکربندی مالی (فاز ۵)

## ۱. قراردادهای واقعی Backend (منبع استخراج)

| حوزه | Endpoint | Permission |
| --- | --- | --- |
| درخت حساب‌ها | `GET /Accounts/tree` → `AccountTreeNodeDto[]` (عمق متغیر) | `accounting:accounts:read` |
| فرزندان/مسیر والد | `GET /Accounts/{id}/children` · `GET /Accounts/{id}/breadcrumb` | read |
| قابل‌ثبت‌ها | `GET /Accounts/postable` | read |
| جست‌وجو/فیلتر | `GET /Accounts?searchTerm&level&nature&isActive&allowPosting&parentAccountId&page&pageSize` | read |
| ایجاد | `POST /Accounts` — `CreateAccountRequest` | `accounts:create` |
| ویرایش | `PUT /Accounts/{id}` — فقط `Name/Description/NormalBalance/Require*` | `accounts:update` |
| غیرفعال/فعال | `POST /Accounts/{id}/deactivate` · `/activate` | `accounts:deactivate` / `accounts:update` |
| حذف | `DELETE /Accounts/{id}` — فقط بدون فرزند و بدون گردش | `accounts:delete` |
| سال مالی | `POST/GET /FiscalYears` + `/open` `/close` | `fiscal-years:create` / `fiscal-years:update` |
| دوره‌ها | `GET {id}/periods` · `POST periods/generate` · `POST periods/custom` · `POST periods/{pid}/open|close` | `fiscal-years:update` / `periods:close` |
| تنظیمات | `GET/PUT /accounting/settings` + `GET history` | `settings:read` / `settings:update` |

## ۲. قیدهای Domain که UI رعایت می‌کند

- **سطوح**: `Group/General/Subsidiary/Detail` از backend — UI هیچ محدودیت چهار سطحی اعمال نمی‌کند؛ درخت بازگشتی عمق هرچقدر API بدهد رندر می‌کند و برچسب سطح از مقدار واقعی هر نود می‌آید (Detail خودش چندسطحی است).
- **ویرایش ناپذیرها**: کد، والد، ماهیت و سطح پس از ایجاد تغییرناپذیرند — در فرم ویرایش فقط نمایش و payload فقط فیلدهای `UpdateAccountRequest`.
- **حذف**: سه قید واقعی با کد خطای backend (`CannotDeleteSystemAccount` / `CannotDeleteAccountWithChildren` / `CannotDeleteAccountWithFinancialActivity`) — در پنل جزئیات همیشه نمایان (`delete-constraints`)، دکمه حذف با `hasChildren` disable و خطای backend در toast با پیام فارسی اصلی.
- **Validation فرم**: پیام‌ها عین `AccountValidators.cs` — کد: فقط رقم ≤20 (`^[0-9]+$`)، نام الزامی ≤200، توضیحات ≤500.

## ۳. تبدیل تقویم فقط برای نمایش

همه مرزهای تاریخ مالی (سال/دوره/فیلترها) به‌صورت ISO DateOnly از API خوانده و به API ارسال می‌شوند؛ `formatToPersianDate` فقط نمایش است و تاریخ مالی را تغییر نمی‌دهد. شروع/پایان سال در ایجاد سال مالی اختیاری است — محاسبه بر عهده backend (تقویم مالی تنظیمات).

## ۴. تنظیمات شرکت — نگاشت فیلدهای خواسته‌شده به AccountingSettingsDto

- ارز پایه → `defaultCurrencyCode` · دقت اعشار → `decimalPlaces` (۰–۷) · گرد کردن → `roundingMode` (۴ حالت)
- تقویم مالی → `calendarType` + `fiscalYearStartMonth/Day`
- شماره‌گذاری → `autoNumberingEnabled` + `voucherNumberingPolicy` (متوالی/ماهانه/روزانه)
- تأیید اسناد → `approvalPolicy` (مستقیم/دومرحله‌ای/سه‌مرحله‌ای) + `requireSeparateApprover/Poster` + `allowUnbalancedDraft`
- مانده منفی → `allowNegativeBalancesPolicy` · قفل اسناد → `lockDate`
- ذخیره با `ChangeReason` + `version` (optimistic concurrency — تعارض 409 → پیام و refresh) · تاریخچه نسخه‌ها از `GET history`

## ۵. Gaps مستندشده (نیاز Backend)

1. **الگوی کدینگ قابل‌تنظیم**: تنظیمات backend فیلد الگوی کدینگ ندارد؛ طول کد فقط ≤20 رقم با validation عمومی است. قید سطح‌بندی در Domain ثابت است (4 enum) اما Detail چندسطحی را پوشش می‌دهد.
2. **ویرایش سال مالی**: `UpdateFiscalYearRequest` در Domain وجود دارد ولی endpoint PUT برای آن expose نشده — ویرایش نام سال فعلاً API ندارد.
3. **تنظیمات تفصیلی‌ها به‌صورت مرکزی**: تفصیلی‌ها فقط per-account (`require*`) هستند؛ ماژول مرکزی مرکز هزینه/پروژه هنوز CRUD UI ندارد (قید در سند اعمال می‌شود).
4. **قالب‌های کدینگ**: `defaultAccountTemplateId` در تنظیمات هست ولی endpoint عمومی برای لیست قالب‌ها پیدا نشد — نمایش «—» برای null.

## ۶. تست‌های فاز ۵ (۲۹ سناریو — مجموع ۱۴۵ سبز)

درخت عمق۵ (Detail فرزند‌دار) · expand/collapse · جست‌وجوی بین‌عمقی · فیلترها · postable با حفظ مسیر والد (hasPostingDescendant بازگشتی) · integration صفحه (tree→detail→breadcrumb→children) · validation فرم با پیام‌های backend · payload واقعی Create/Update · کد کامل پیش‌بینی · قید ویرایش ناپذیر · محدودیت‌های حذف inline + خطای backend در toast · مجوزها (بدون دکمه) · سال مالی list/create validate/payload · دوره‌ها + badge تعدیلات · close دوره gated با `periods:close` · settings رندر کامل فیلدها · PUT با version+changeReason · تعارض 409 → refresh · تاریخچه · read-only/update-less gating.
