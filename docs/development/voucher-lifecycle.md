# چرخه عمر سند حسابداری و موتور ثبت قطعی (Voucher Lifecycle & Posting Engine)

این سند مرجع کامل معماری، قواعد و API موتور اسناد حسابداری دوبل (PROMPT 04) است.

---

## ۱. مدل سند (AccountingDocument)

سند حسابداری Aggregate Root ماژول Accounting است و تمام اطلاعات منبع یک تراکنش مالی را نگه می‌دارد:

| فیلد | نوع | توضیح |
| :--- | :--- | :--- |
| `TenantId / CompanyId` | Guid | مالکیت چندمستاجره؛ توسط فیلترهای سراسری و گاردهای جهش داده محافظت می‌شود |
| `FiscalYearId / FiscalPeriodId` | Guid | سال و دوره مالی سند؛ در زمان ایجاد از تاریخ سند resolve می‌شود |
| `DocumentNumber` | long | شماره سند یکتا در سال مالی (ایندکس یکتا در سطح پایگاه‌داده) |
| `DocumentDate` | DateOnly | تاریخ سند؛ باید داخل سال/دوره مالی باز باشد |
| `SourceModule / SourceDocumentId` | string? / Guid? | منشأ سیستمی سند (Manual, Sales, Purchasing, Inventory, Treasury, Payroll) |
| `Status` | DocumentStatus | وضعیت چرخه عمر (بخش ۲) |
| `TotalDebit / TotalCredit` | decimal(28,4) | جمع بدهکار/بستانکار بر اساس ارز پایه شرکت؛ با تغییر ردیف‌ها بازمحاسبه می‌شود |
| `IdempotencyKey` | string? (یکتا) | کلید ایدمپوتنسی برای جلوگیری از ثبت تکراری در سطح شرکت |
| `CreatedBy / PostedBy` + زمان‌ها | string / DateTimeOffset? | کاربر واقعی ایجادکننده و قطعی‌کننده با زمان دقیق |
| `Version` | uint (Concurrency Token) | قفل خوش‌بینانه برای کشف تغییرات هم‌زمان |

### ردیف‌های سند (AccountingDocumentLine)

- تمام مبالغ `decimal` با دقت (۲۸,۴) و نرخ تسعیر با دقت (۱۸,۶) ذخیره می‌شوند.
- هر ردیف فقط یک سمت دارد (`DebitCredit` enum)؛ **مبلغ بدهکار و بستانکار هم‌زمان مثبت ممنوع** (کنترل دامنه + موتور + داده مخدوش).
- ردیف بدون مبلغ معتبر (صفر یا منفی) در زمان ایجاد رد. نرخ تسعیر باید مثبت و مبلغ ارزی بدون کد ارز مجاز نیست.
- ابعاد تحلیلی هر ردیف: `CostCenterId`, `ProjectId`, `DetailAccountId`.

## ۲. وضعیت‌ها و ماشین وضعیت صریح

```text
            ┌──────────────► (Reject) ──────────────┐
            │                                       ▼
  Draft ──► PendingApproval ──► Reviewed ──► Approved ──► Posted ──► Reversed
    │                            ▲                              (با سند معکوس)
    └── (Post مستقیم، فقط سیاست DirectPostingAllowed) ─────────────┘
```

| وضعیت | معنا | قابل ویرایش/حذف |
| :--- | :--- | :---: |
| `Draft` | پیش‌نویس | ✅ |
| `PendingApproval` | ارسال‌شده به جریان تأیید | ❌ |
| `Reviewed` | بررسی‌شده (مرحله میانی سیاست سه‌مرحله‌ای) | ❌ |
| `Approved` | تأییدشده، آماده قطعی‌سازی | ❌ |
| `Posted` | ثبت قطعی — غیرقابل تغییر، اصلاح فقط با Reverse/Adjustment | ❌ |
| `Reversed` | باطل‌شده با سند معکوس (Storno) | ❌ |

تمام انتقال‌ها فقط از طریق کلاس `AccountingDocumentWorkflow` (در `Domain/Services`) مجازند و هر انتقال غیرمجاز با `InvalidDocumentTransitionException` رد می‌شود. این ماشین وضعیت Pure و کاملاً Unit-Test پذیر است.

### سیاست تأیید شرکت (DocumentApprovalPolicy)

| سیاست | مسیر مجاز قطعی‌سازی |
| :--- | :--- |
| `DirectPostingAllowed` | `Draft → Posted` مستقیم؛ مسیر تأیید اختیاری |
| `TwoStepApprovalRequired` | `Draft → PendingApproval → Approved → Posted` |
| `ThreeStepApprovalRequired` | `Draft → PendingApproval → Reviewed → Approved → Posted` |

سیاست از `AccountingSettings.ApprovalPolicy` شرکت خوانده می‌شود و هم در `Approve` و هم در `Post` توسط موتور اعمال می‌گردد.

## ۳. قوانین Double Entry (کنترل یکپارچگی مالی)

قطعی‌سازی سند تنها زمانی مجاز است که:

1. سند حداقل دو ردیف معتبر (یک بدهکار و یک بستانکار) داشته باشد؛ سند بدون ردیف قطعی نمی‌شود.
2. **جمع بدهکار دقیقاً برابر جمع بستانکار باشد** — اختلاف حتی یک واحد کوچک‌تر از دقت (۰٫۰۰۰۱) ممنوع؛ محاسبات `decimal` است و خطای گرد کردن ندارد.
3. هیچ ردیفی مبلغ صفر یا بدهکار/بستانکار هم‌زمان نداشته باشد.
4. همه حساب‌های ردیف‌ها فعال، دارای مجوز ثبت (`AllowPosting` در سطوح معین/تفصیلی) و متعلق به **همان شرکتِ سند** باشند.
5. سال مالی و دوره مالی سند «باز» باشند و تاریخ سند داخل محدوده هر دو قرار گیرد.
6. تراز بر اساس ارز پایه شرکت کنترل می‌شود؛ مبالغ ارزی ردیف‌ها اطلاعاتی‌اند.

## ۴. موتور ثبت قطعی اتمیک (VoucherPostingEngine)

`PostAsync` در یک تراکنش دیتابیس این مراحل را انجام می‌دهد و **هر شکست در هر مرحله باعث Rollback کامل می‌شود**:

1. **قفل سطر سند**: روی PostgreSQL سند با `SELECT ... FOR UPDATE` قفل می‌شود تا دو درخواست هم‌زمان نتوانند یک سند را قطعی کنند.
2. **بررسی مجدد سند**: بارگذاری سند + ردیف‌ها؛ اگر قبلاً Posted شده باشد، نتیجه ایدمپوتنت برمی‌گردد.
3. **بررسی وضعیت دوره مالی**: سال باز، دوره باز، تاریخ داخل بازه.
4. **بررسی دسترسی**: سند باید متعلق به شرکت فعال کاربر باشد؛ سند شرکت/مستاجر دیگر NotFound است.
5. **بررسی حساب‌ها**: فعال بودن، مجوز ثبت، تعلق شرکت.
6. **کنترل تراز**: توازن دقیق بر اساس ارز پایه.
7. **کنترل شماره سند**: شماره در زمان ایجاد از `VoucherSequence` صدور شده و یکتایی آن با ایندکس یکتای پایگاه‌داده تضمین می‌شود.
8. **ثبت وضعیت قطعی + ممیزی**: `Posted` + `PostedBy/PostedAtUtc` + رکورد `DocumentAuditLog`.
9. **ایجاد دفتر کل**: به ازای هر ردیف یک `GeneralLedgerEntry` با ارجاع کامل (سند، ردیف، منشأ، دوره) نوشته می‌شود.

### گاردهای ضد ثبت تکراری (سه لایه)

| لایه | سازوکار |
| :--- | :--- |
| قفل ردیف | `FOR UPDATE` روی PostgreSQL — درخواست دوم منتظر commit اول می‌ماند و سپس نتیجه ایدمپوتنت می‌گیرد |
| ایندکس یکتای دفتر کل | `acc_gl_entries (tenant_id, company_id, document_id, document_line_id)` — حتی در برابر باگ یا داده مخدوش، نوشتن دوم ستون دفتر کل مستقیماً در پایگاه‌داده شکست می‌خورد |
| کلید ایدمپوتنسی | ایندکس یکتای `IdempotencyKey` در سطح شرکت؛ تکرار با کلید یکسان نتیجه عملیات اول را برمی‌گرداند و کلید متفاوت با `IdempotencyKeyConflictException` رد می‌شود |

### اصلاح اسناد قطعی

سند Posted **هرگز** مستقیماً Update/Delete نمی‌شود (کنترل در دامنه، سرویس و گاردهای پایگاه‌داده). اصلاح تنها از دو مسیر مجاز است:

- **Reverse**: ایجاد سند معکوس آینه‌ای که بلافاصله قطعی می‌شود و سند مبدأ `Reversed` می‌گردد؛ درخواست معکوس‌سازی تکراری، سند معکوس موجود را بازمی‌گرداند.
- **Adjustment**: ایجاد سند تعدیل جدید (DocumentType=Adjustment) با منشأ مشخص.

## ۵. اسناد سیستمی و قرارداد Source Reference

اسناد می‌توانند منشأ داشته باشند: `Manual`، `Sales`، `Purchasing`، `Inventory`، `Treasury`، `Payroll` (به‌علاوه `Accounting` برای اسناد معکوس داخلی). قرارداد ارتباطی ماژول‌های دیگر:

```csharp
IAccountingModuleApi.CreateVoucherFromSourceAsync(new CreateVoucherFromSourceRequest
{
    SourceModule = "Sales",          // منشأ اجباری و استاندارد
    SourceDocumentId = invoiceId,    // شناسه رکورد مبدأ برای ردیابی کامل
    ...
    AutoPost = true,                 // ایجاد + قطعی‌سازی اتمیک
});
```

- کلید ایدمپوتنسی پیش‌فرض اسناد سیستمی: `src:{SourceModule}:{SourceDocumentId}` — بازآرای همان سند عملیاتی هرگز سند دوم نمی‌سازد.
- ردیابی معکوس: `GetVouchersBySourceAsync(tenantId, companyId, sourceModule, sourceDocumentId)`.
- رویداد یکپارچگی `AccountingVoucherPostedIntegrationEvent` برای اطلاع‌رسانی به ماژول‌های دیگر منتشر می‌شود.

> ماژول‌های Sales/Purchasing/Inventory/Treasury/Payroll فعلاً پیاده‌سازی نشده‌اند؛ فقط این قرارداد آماده است.

## ۶. API

Base route: `/api/v1/accounting/documents` — همه عملیات نیازمند هدرهای `X-Tenant-Id` و `X-Company-Id` و احراز هویت JWT هستند.

| عملیات | متد و مسیر | Permission |
| :--- | :--- | :--- |
| ایجاد پیش‌نویس | `POST /` | `accounting:documents:create` |
| ویرایش پیش‌نویس | `PUT /{id}` | `accounting:documents:update` |
| حذف پیش‌نویس | `DELETE /{id}` | `accounting:documents:delete` |
| مشاهده سند | `GET /{id}` | `accounting:documents:read` |
| جستجو و صفحه‌بندی | `GET /?status=&fromDate=&toDate=&sourceModule=&...` | `accounting:documents:read` |
| سابقه ممیزی | `GET /{id}/audit-trail` | `accounting:documents:read` |
| ارسال به تأیید | `POST /{id}/submit` | `accounting:documents:submit` |
| تأیید | `POST /{id}/approve` | `accounting:documents:approve` |
| رد (با دلیل اجباری) | `POST /{id}/reject` | `accounting:documents:reject` |
| ثبت قطعی | `POST /{id}/post` + هدر `Idempotency-Key` | `accounting:documents:post` |
| معکوس‌سازی | `POST /{id}/reverse` | `accounting:documents:reverse` |

منطق کسب‌وکار فقط در Application Services و موتور سند است؛ Controller صرفاً Command می‌سازد و سرویس را صدا می‌زند.

## ۷. امنیت و ممیزی

- مجوزهای تفکیک‌شده برای ایجاد، ویرایش، حذف، ارسال، تأیید، رد، قطعی‌سازی و معکوس‌سازی (بخش ۶) با `[HasPermission]`.
- بررسی عضویت شرکت کاربر در `TenantResolutionMiddleware` (خطای `Tenancy.ForbiddenMembership`) + گارد دفاعی شرکت در موتور.
- گاردهای جهش داده `BaseDbContext`: ذخیره موجودیت با TenantId/CompanyId بیگانه با `SecurityException` مسدود می‌شود (جلوگیری از دستکاری شناسه‌ها).
- ردیف‌های دفتر کل و اسناد با فیلترهای سراسری مستاجره‌بندی شده‌اند؛ داده مستاجر دیگر به‌کلی نامرئی است.
- Audit Trail کامل در `acc_document_audit_logs` برای تمام عملیات چرخه عمر با کاربر واقعی و زمان دقیق؛ این رکوردها حتی پس از حذف پیش‌نویس باقی می‌مانند (بدون FK به سند).

## ۸. Migration و اجرای تست‌ها

- Migration ماژول: `src/ERP.Modules.Accounting/Migrations/20261115075703_AddVoucherEngineAndGeneralLedger.cs` (جدول‌های `acc_documents`, `acc_document_lines`, `acc_gl_entries`, `acc_document_audit_logs` و ایندکس‌های یکتا).
- تولید Migration جدید: `dotnet-ef migrations add <Name> --project src/ERP.Modules.Accounting` (کارخانه طراحی `AccountingDbContextFactory`).
- تست‌های هم‌زمانی روی PostgreSQL واقعی (`VoucherConcurrencyPostgresTests`): با متغیر محیطی `ERP_TEST_POSTGRES` (رشته اتصال) یا سرور محلی روی `localhost:5433` (کاربر `erp_user`) اجرا می‌شوند؛ در نبود سرور با پیام توضیحی رد می‌شوند. برای راه‌اندازی سریع: `docker compose up -d postgres`.
