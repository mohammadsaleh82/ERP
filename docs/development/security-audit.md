# ممیزی امنیت حسابداری (PROMPT 09)

## ۱. Permission Matrix — ماتریس ۲۰ مجوز مستقل در `AccountingPermissions` با تست پوشش reflection (`PermissionMatrixTests`). اعمال روی Tenant/Company/Brananch.

## ۲. Segregation of Duties (قابل تنظیم از تنظیمات شرکت)
- `RequireSeparateApprover`: ثبت‌کننده سند نمی‌تواند سند خودش را تأیید کند → خطای دامنه `Accounting.SodCreatorCannotApprove`.
- `RequireSeparatePoster`: تأییدکننده نمی‌تواند همان سند را قطعی کند → `Accounting.SodApproverCannotPost`.
- بستن سال مالی مجوز مستقل (`fiscalyear:close`) و بازگشایی نیازمند تأییدکننده متمایز + مجوز مستقل دارد (از PROMPT چرخه مالی، تست‌شده).
- تست‌ها: `SegregationOfDutyTests` (مسدودسازی، اجازه با سیاست خاموش، پایداری تنظیم).

## ۳. Audit Trail (دفتر ممیزی عمومی)
- جدول `acc_audit_trail` + سرویس `IAccountingAuditTrailService` (فقط Record و Query — بدون مسیر Update/Delete؛ تست reflection).
- هوک‌شده در: ایجاد/ویرایش/حذف پیش‌نویس، ارسال، تأیید، رد (DocumentService)؛ قطعی‌سازی و معکوس‌سازی (موتور سند، پس از Commit)؛ ایجاد/تغییر/غیرفعال‌سازی حساب؛ تغییر تنظیمات؛ بستن دوره/سال؛ بازگشایی سال.
- هر رکورد: UserId، Tenant/Company/Branch، Action، EntityType/Id، Timestamp، **CorrelationId** (هدر X-Correlation-Id)، Reason و Before/After Snapshot.
- **پاک‌ساز Snapshot**: کلیدهای حساس (password/token/secret/apikey/credential/idempotencykey) → `[REDACTED]` + سقف طول ۸۰۰۰ (تست‌شده).
- غیرقابل تغییر: بدون API تغییر/حذف + Trigger PG و Role محدود (`scripts/db/postgres-security.sql`).

## ۴. Tenant Security — یافته‌ها و اصلاحات
- **[رفع‌شده] Cross-Tenant Read در GetDocumentAsync**: فیلتر Tenant نداشت؛ حالا `TenantId != tenantId → NotFound`.
- **[رفع‌شده] SearchDocumentsAsync بدون فیلتر Tenant/Company** → اضافه شد.
- تفکیک شعبه: کاربر با `ActiveBranchId` سند شعبه دیگر را نمی‌بیند (NotFound).
- IDOR/درهم‌سازی شناسه‌ها: تست `AccountingTenantSecurityTests` (سند شرکت/Tenant دیگر، کارت حساب شرکت دیگر، Mass Assignment).
- حفاظت‌های موجود تأییدشده: قفل FOR UPDATE + کنترل شرکت در موتور، Mutation Guard، سیاست‌های ابعاد، IdempotencyKey یکتای DB.

## ۵. Database Security
- FKها: GL→Account، GL→DocumentLine و قید یکتایی (Tenant,Company,DocumentId,DocumentLineId) موجود؛ ایندکس‌های Tenant-aware کامل.
- **RLS Strategy**: `scripts/db/postgres-security.sql` — RLS روی همه جداول acc_* با `current_setting('app.tenant_id')`. الگوی امن با Pooling: `SET LOCAL` داخل تراکنش (ریست خودکار) — **تست‌شده با PostgreSQL واقعی** (`RowLevelSecurityPostgresTests`): خوانش بین‌راهی ۲ Tenant ایزوله + عدم نشت در اتصال بازگشته به Pool (۱۰ دور).
- Migration Security: مهاجرت‌ها بدون Secret؛ اجرای مهاجرت با نقش مهاجرت و DML با `erp_app`.
- Safe Query Construction: همه پرس‌وجوها LINQ/EF (پارامتری)؛ FromSqlRaw فقط با پارامتر `{0}` (قفل FOR UPDATE).

## ۶. Concurrency (PostgreSQL واقعی — `AccountingConcurrencyPostgresTests`)
- ویرایش هم‌زمان پیش‌نویس: Concurrency Token `Version` → دقیقاً یک برنده.
- قطعی‌سازی هم‌زمان: قفل FOR UPDATE + قید یکتای GL + idempotency (تست موجود `VoucherConcurrencyPostgresTests`).
- **معکوس‌سازی هم‌زمان**: فقط یک سند معکوس ساخته می‌شود.
- **بستن دوره در برابر قطعی‌سازی**: قفل ردیف دوره (`FOR UPDATE` مرحله ۳-الف PostAsync) + اعتبارسنجی دوره داخل تراکنش → سند قطعی در دوره بسته ثبت نمی‌شود (ناوردایی تست‌شده).
- شماره سند هم‌زمان: Sequence با قید یکتا (تست موجود).

## ۷. امنیت عملیاتی
- Rate Limiting: پنجره ثابت ۱۰۰ درخواست/۱۰ ثانیه بر IP (429) — تست `RateLimitingEndpointTests`.
- Security Headers: nosniff/DENY/Referrer-Policy/CSP — تست `SecurityHeadersEndpointTests`.
- Exception Sanitization: ProblemDetails بدون Stack Trace — تست‌شده.
- CorrelationId در دفتر ممیزی + Serilog LogContext.

## تهدیدهای شناسه‌شده و وضعیت
| تهدید | وضعیت |
| --- | --- |
| Cross-tenant read via GetDocument | رفع شد + تست |
| ردیف مالی تکراری با Post هم‌زمان | مسدود (قفل+قید) + تست |
| سند قطعی در دوره بسته (مسابقه) | مسدود (قفل دوره) + تست |
| سند معکوس دوبله | مسدود + تست |
| دستکاری Tenant/Company در Body | مسدود (LoadCompanyDocument/فرمان‌های زمینه‌محور) + تست |
| نشت Context در Pool با RLS | تست‌شده امن (SET LOCAL) |
| تغییر/حذف Audit | مسدود (Append-Only API + Trigger/Role) |
| نشت داده حساس در Snapshot | پاک‌ساز + تست |
