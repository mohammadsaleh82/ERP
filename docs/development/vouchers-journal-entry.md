# اسناد حسابداری و تجربه ثبت سند (فاز ۶)

## ۱. قراردادهای واقعی Backend (منبع: DocumentDtos.cs + DocumentsController.cs)

| عملیات | Endpoint | Permission | یادداشت |
| --- | --- | --- | --- |
| ایجاد | `POST /Documents` | `documents:create` | `IdempotencyKey` + `DocumentType` |
| ویرایش پیش‌نویس | `PUT /Documents/{id}` | `documents:update` | فقط Draft (غیر آن `DocumentNotEditable`) |
| حذف پیش‌نویس | `DELETE /Documents/{id}` | `documents:delete` | فیزیکی، فقط Draft |
| مشاهده | `GET /Documents/{id}` | `documents:read` | `DocumentDetailDto` با `IsBalanced` و Lines |
| جست‌وجو | `GET /Documents?status&fiscalYearId&fromDate&toDate&sourceModule&documentNumber&pageNumber&pageSize&sortBy` | `documents:read` | PagedRequest با MaxPageSize=100 |
| تاریخچه | `GET /Documents/{id}/audit-trail` | `documents:read` | `DocumentAuditLogDto[]` |
| ارسال/تأیید/رد | `POST {id}/submit` · `/approve` · `/reject{rejectionReason}` | submit/approve/reject | رد با دلیل الزامی ≤۵۰۰ |
| ثبت قطعی | `POST {id}/post` → `PostDocumentResultDto` | `documents:post` | conflict → 409 |
| معکوس | `POST {id}/reverse{reversalDate,description?,IdempotencyKey?}` | `documents:reverse` | خروجی شماره سند معکوس |

**وضعیت‌ها**: Draft → PendingApproval → Reviewed → Approved → Posted → Reversed.
**قواعد SOD واقعی**: `SodCreatorCannotApprove` و `SodApproverCannotPost` — پیام فارسی در toast.
**خطاهای Domain نمایش‌داده‌شده**: `DocumentDateOutsideOpenPeriod/FiscalYear` (دوره بسته)، `InvalidLineAmount`، `DimensionRequired/NotAllowed`، `PostingConcurrencyConflict` (409 → پیام + refetch)، `DocumentNotEditable`، `IdempotencyKeyConflict`، `EmptyRejectionReason`.

## ۲. سیاست مبلغ — Decimal-safe

- تمام محاسبه جمع/اختلاف با **decimal.js** (رشته decimal؛ بدون Float). `0.1+0.2='0.3'` در تست.
- ورودی کاربر: ارقام فارسی/عربی، ٬/,/٫ → `parseAmountInput` → رشته تمیز؛ `NaN` رد می‌شود.
- سمت سرور decimal از **number** می‌بندد (بدون JsonNumberHandling)؛ ارسال با `toApiNumber` و **round-trip guard**: اگر دقت در تبدیل حفظ نشود خطای صریح (سقف مجاز سرور 999_999_999_999_999 < 2^53 امن است).
- فرمت نمایش: جداکننده هزارگان؛ صفر → «—»؛ اختلاف متوازن → «0».

## ۳. UX ثبت سند

- جدول ردیف‌ها با ویرایش درجا، افزودن/کپی/حذف/جابه‌جایی (فقط پیش‌نویس — ترتیب با جایگزینی کل ردیف‌ها به backend می‌رسد).
- انتخاب حساب با جست‌وجوی زنده روی `GET /Accounts/postable` واقعی + نشان فلاگ‌های تفصیلی الزامی.
- سمت ردیف با ستونی که مبلغ در آن پر می‌شود تعیین می‌شود (بدهکار/بستانکار) و به `Side: Debit|Credit` سرور نگاشت می‌شود.
- جمع بدهکار/بستانکار/اختلاف زنده؛ بج متوازن/نامتوازن.
- کنترل اعتبار pre-send مطابق validatorهای backend: شرح/تاریخ/ردیف ناقص/مبلغ غیرمثبت/سقف/سند نامتوازن/تفصیلی الزامی + هشدار «دوره بسته» از دوره‌های واقعی سال انتخابی.
- دکمه‌ها با `isPending` غیرفعال می‌شوند (بدون کلیک تکراری)؛ همه عملیات حساس ConfirmDialog دارند.

## ۴. لیست اسناد

فیلترهای واقعی سرور (وضعیت/از/تا/شماره سند) + `pageNumber`/`sortBy` — **همه در querystring** با `updateParams` یکجا (بدون stale closure)؛ قابل اشتراک‌گذاری و بازگشت. مرتب‌سازی با toggle ستون (DocumentNumber/DocumentDate) و جهت asc/desc. جزئیات سریع Drawer.

## ۵. مشاهده سند

- سند **Posted هرگز قابل ویرایش رندر نمی‌شود** (نتیجه post → refetch → نود read-only با یادداشت) — تست سبز.
- Draft با مجوز update دکمه ویرایش → همان فرم با خطوط موجود.
- تاریخچه audit-trail در Drawer با گذار وضعیت‌ها؛ Reversed نشان «باطل‌شده»؛ RejectionReason در Draft نمایان.

## ۶. چاپ

قالب A4 راست‌چین در `VoucherPrint` با `hidden print:block` + قاعده `@media print` سراسری (فقط `.voucher-print` visible؛ `@page A4/10mm`). جمع‌ها از `totalDebit/totalCredit` سرور (decimal-safe) و اعداد تبلار.

## ۷. تست‌ها (۳۲ مورد فاز ۶ — مجموع ۱۷۷ سبز)

Decimal-safe (۵) · فرم تعاملی (۱۱): توازن زنده/نامتوزان 0.01/ردیف ناقص/مبلغ منفی/DimensionRequired/دوره بسته/payload واقعی با ارقام فارسی و idempotency/کپی-جابه‌جایی-حذف/خطای شبکه/خطای اعتبارسنجی backend/ویرایش PUT · لیست (۵): فیلترها در URL+params سرور/مرتب‌سازی toggle/صفحه‌بندی/خطا · جزئیات+گردش کار (۱۱): posted read-only/draft edit/تأیید+SOD/رد با دلیل الزامی/post 409 تعارض+refresh/موفقیت post→read-only/reverse payload/audit-trail/چاپ/بدون مجوز.
E2E: `tests/e2e/voucher.spec.ts` — سناریوی ایجاد سند متوازن (نیازمند `npx playwright install chromium`؛ اجرا: `npm run test:e2e`).

## ۸. Gaps

1. **Export**: endpoint خروجی (Excel/PDF) در backend وجود ندارد — خروجی ارائه نشده؛ چاپ فقط client-side.
2. **انتخاب تفصیلی در ردیف**: VoucherLineInput فیلدها را می‌پذیرد ولی ماژول CRUD ابعاد (مرکز هزینه/پروژه/تفصیلی) endpoint لیست ندارد → ردیف فقط «الزام» را نشان می‌دهد و backend در ذخیره اعتبارسنجی می‌کند (`DimensionRequired`).
3. **مرحله Reviewed**: endpoint مستقل review در controller نیست؛ گذار با سیاست تأیید backend انجام می‌شود (approve در سیاست سه‌مرحله‌ای به Reviewed می‌رسد) — UI گذار را در audit-trail نشان می‌دهد.
