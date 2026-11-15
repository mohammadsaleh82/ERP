# ابعاد مالی و تفصیلی شناور (Financial Dimensions & Floating Accounts)

مرجع کامل معماری ابعاد مالی (PROMPT 06). هدف: ثبت و گزارش‌گیری مالی بر اساس تفصیلی و ابعاد مالی **بدون نقض مالکیت ماژول‌ها**.

---

## ۱. مفاهیم پایه و مالکیت داده

| مفهوم | مالک داده | نحوه حضور در حسابداری |
| :--- | :--- | :--- |
| Party / Customer / Supplier | ماژول **Parties** | آینه مرجع (`ExternalReferenceId` + `OwnerModule`) |
| Employee | ماژول **Payroll** | آینه مرجع |
| Contract / سایر مفاهیم عملیاتی | ماژول عملیاتی مربوطه | آینه مرجع |
| CostCenter / Project / Branch / DetailAccount / Custom | خود حسابداری | رکورد داخلی رجیستری ابعاد |

- حسابداری هرگز نسخه مستقل و ناسازگار از داده ماژول مالک نمی‌سازد؛ فقط «شناسه + کد + نام + وضعیت فعال» را به‌صورت آینه نگه می‌دارد و با قرارداد ماژول همگام می‌شود.
- ماژول Parties و Project در این فاز پیاده‌سازی نشده‌اند؛ قرارداد و زیرساخت آماده پذیرش آن‌هاست.

## ۲. Dimension Framework

موجودیت مرکزی: `FinancialDimension` (`acc_financial_dimensions`)

| فیلد | توضیح |
| :--- | :--- |
| `DimensionType` | DetailAccount، CostCenter، Project، Branch، Contract، Party، Customer، Supplier، Employee، Custom |
| `Id` | شناسه بعد مالی در حسابداری |
| `Code / Name` | کد یکتا به ازای (شرکت، نوع) و نام |
| `CompanyId` | مالکیت شرکت (چندمستاجره) |
| `IsActive` | بعد غیرفعال در اسناد جدید قابل استفاده نیست |
| `ExternalReferenceId / OwnerModule` | برای ابعاد متعلق به ماژول‌های دیگر |

### ردیابی روی اسناد و دفتر کل

- `acc_document_line_dimensions`: اختصاص بعد به **ردیف سند** — از هر نوع بعد حداکثر یک مقدار به ازای هر ردیف.
- `acc_gl_entry_dimensions`: انعکاس ابعاد روی **سطر دفتر کل** در همان تراکنش قطعی‌سازی؛ مبنای همه گزارش‌های تحلیلی.
- سند معکوس ابعاد سند اصلی را کامل منعکس می‌کند (شامل ابعاد سفارشی).
- ستون‌های قدیمی (`CostCenterId/ProjectId/DetailAccountId`) به اختصاص‌های جدید نرمال می‌شوند؛ منبع حقیقت واحد، جدول اختصاص است و تضاد ستون قدیمی با اختصاص جدید خطای دامنه است (`DimensionLegacyConflict`).

## ۳. Dimension Rules (قواعد در Domain/Application)

موتور قواعد خالص در `Domain/Services/DimensionValidationService` و پیاده‌سازی بارگذاری داده در `Application/Services/DimensionValidationApplicationService`:

1. **حساب نیازمند تفصیلی باشد** → سیاست `Required` یا پرچم قدیمی `RequireDetailAccount` (و معادل CostCenter/Project).
2. **حساب بدون تفصیلی قابل ثبت نباشد** → همان قاعده Required؛ رد در Draft **و** مجدد در قطعی‌سازی (دفاع در عمق).
3. **حساب فقط نوع مشخصی را قبول کند** → سیاست `Allowed` (لیست سفید).
4. **ترکیب چند Dimension الزامی باشد** → چند سیاست `Required` هم‌زمان؛ همه باید حاضر باشند.
5. **ترکیب‌های ممنوع** → `DimensionCombinationRule` در سطح «نوع» یا «مقدار» (دو طرف یا نوع‌اند یا مقدار).
6. **بعد شرکت دیگر** → `DimensionForeignCompany` (بارگذاری با `IgnoreQueryFilters` + فیلتر صریح Tenant تا خطای اختصاصی صادر شود).
7. **بعد غیرفعال** → `DimensionInactive`.
8. حساب متعلق به شرکت دیگر از اعتبارسنجی ابعاد کنار گذاشته می‌شود تا اعتبارسنج حساب‌ها در مسیر قطعی‌سازی خطای رسمی خودش را بدهد.

خطاهای دامنه: `DimensionNotFound / DimensionInactive / DimensionForeignCompany / DimensionRequired / DimensionNotAllowed / DimensionNotInAllowedSet / DimensionCombinationForbidden / DimensionTypeMismatch / DimensionLegacyConflict / DimensionDuplicateAssignment`.

## ۴. Financial Analysis (تحلیل مالی چندبعدی)

`DimensionAnalysisService` — Query Side جدا از Write Model، مبتنی بر `acc_gl_entry_dimensions`:

- **گردش بر اساس نوع بعد**: مشتری، تأمین‌کننده، پروژه، مرکز هزینه، شعبه (شعبه از ستون `BranchId` خود سطر GL بدون جدول ابعاد) و سایر انواع — با بدهکار/بستانکار/مانده علامت‌دار + جهت و مقدار نمایشی (سياست موتور مانده PROMPT 05).
- **گردش یک حساب به تفکیک بعد** (مثلاً گردش حساب پرداختنی بر اساس تأمین‌کننده).
- **گزارش ترکیبی چندبعدی**: تجمیع بر اساس ترکیب ۱ تا ۳ نوع بعد؛ سقف ردیف (`MaxRows`، حداکثر ۵۰۰۰) + پرچم `IsTruncated` + گارد سقف داده ورودی (۵۰ هزار سطر GL) برای کنترل حجم؛ ابعاد فقط برای سطرهای فیلترشده واکشی می‌شود (بدون Join غیرضروری).

## ۵. Integration Contracts (بدون رفرنس متقابل)

- قرارداد: `Contracts/Interfaces/IFinancialDimensionApi` — `RegisterAsync` (Upsert Idempotent با مرجع خارجی)، `SyncAsync` (همگام‌سازی کد/نام/فعال)، `IsActiveAsync`، `GetByExternalReferenceAsync`.
- ماژول مالک فقط این قرارداد را می‌شناسد؛ **هیچ رفرنس پروژه‌ای بین ماژول‌ها وجود ندارد** (تست معماری `ModuleDependencyTests` ناظر است). الگوی اتصال: آداپتور در Composition Root (ERP.Api).
- همچنین `VoucherLineFromSource` (قرارداد اسناد سیستمی) اکنون ابعاد را هم می‌پذیرد تا اسناد ماژول‌های دیگر با ابعاد کامل ثبت شوند.

## ۶. Performance

- ایندکس‌ها: یکتایی `(Tenant, Company, Type, Code)` روی ابعاد؛ محور تحلیلی `(Tenant, Company, DimensionType, DimensionId)` روی GL dimensions؛ یکتایی `(Line, Type)` و `(GLEntry, Type)`؛ یکتایی `(Account, DimensionType)` روی سیاست‌ها.
- بارگذاری دسته‌ای ابعاد/حساب‌ها/قواعد در اعتبارسنجی (بدون N+1)، پروجکشن فقط ستون‌های موردنیاز، صفحه‌بندی واقعی در لیست و تحلیل.
- گزارش ترکیبی: گروه‌بندی پس از فیلتر SQL، سقف ردیف خروجی + سقف داده ورودی.

## ۷. API خلاصه (`api/v1/accounting/dimensions`)

| عملیات | مسیر | Permission |
| :--- | :--- | :--- |
| ایجاد بعد | `POST /` | `accounting:dimensions:write` |
| فهرست / جزئیات | `GET /`, `GET /{id}` | `accounting:dimensions:read` |
| ویرایش نام / فعال / غیرفعال | `PUT /{id}`, `POST /{id}/activate`, `POST /{id}/deactivate` | `accounting:dimensions:write` |
| سیاست‌های حساب | `POST /account-policies`, `GET /account-policies/{accountId}` | write / read |
| قواعد ترکیب ممنوع | `POST /combination-rules`, `GET /combination-rules` | write / read |
| گردش بر اساس بعد | `GET /analysis/turnover` | `accounting:dimensions:read` |
| گردش حساب × بعد | `GET /analysis/accounts/{id}/turnover` | `accounting:dimensions:read` |
| گزارش ترکیبی | `GET /analysis/combined` | `accounting:dimensions:read` |

## ۸. تست‌ها

- `ERP.UnitTests/DimensionValidationTests` (۱۳ تست): قواعد خالص — اجباری، لیست سفید، ممنوع، غیرفعال، شرکت دیگر، نوع ناهمخوان، ترکیب ممنوع نوع/مقدار، چند بعد اجباری.
- `ERP.IntegrationTests/FinancialDimensionIntegrationTests` (۱۲ تست): ۱۱ سناریوی اجباری + گزارش ترکیبی + انعکاس ابعاد در سند معکوس + قرارداد Upsert Idempotent ماژول مالک.
- Migration: `AddFinancialDimensions` (۵ جدول جدید).
