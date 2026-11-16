# چرخه سال مالی: افتتاحیه، اصلاح، معکوس، اختتامیه و بازگشایی

مرجع کامل عملیات چرخه مالی (PROMPT 07). معیار پایان: **اجرای چرخه کامل یک سال از افتتاحیه تا اختتامیه و افتتاحیه سال بعد با حفظ تراز — تست‌شده با ۲۰ داده تصادفی Property-Based.**

---

## ۱. سند افتتاحیه (Opening Engine)

`OpeningVoucherService.CreateOpeningVoucherAsync` — ایجاد و قطعی‌سازی اتمیک سند افتتاحیه سال جدید.

قواعد:
- **منبع مانده‌ها**: سطرهای دفتر کل سال قبل (تنها اسناد قطعی در دفتر کل وجود دارند).
- **حساب‌های دائمی فقط** (Asset/Liability/Equity با AllowPosting)؛ درآمد/هزینه هرگز منتقل نمی‌شوند — نتیجه آن‌ها با اختتامیه به سود و زیان انباشته رفته است.
- **اشخاص و تفصیلی‌ها**: از انعکاس ابعاد سطرهای GL (انواع DetailAccount/Customer/Supplier/Party/Employee)؛ هر گروه (حساب، بعد) یک ردیف افتتاحیه با همان بعد می‌گیرد و باقیمانده حساب به‌صورت ردیف بدون تفصیلی.
- **مانده انبار**: از قرارداد `IOpeningInventorySource` (ماژول Inventory بدون رفرنس مستقیم؛ اتصال در Composition Root). حساب انبار باید دائمی باشد.
- **تراز**: کنترل پیش از ثبت با خطای اختصاصی `OpeningUnbalanced` (اختلاف دقیق گزارش می‌شود) + کنترل موتور Posting.
- **ضدتکرار**: بیش از یک سند Opening برای هر سال ممنوع (`OpeningAlreadyExists`).
- **ترتیب سال‌ها**: سال قبل باید بسته باشد (`OpeningRequiresClosedPreviousYear`) — این گارد جلوی «عدم تطابق افتتاحیه و اختتامیه» را می‌گیرد؛ سال اول بدون سال قبل پشتیبانی نمی‌شود.
- سند به سال جدید تعلق دارد (`FiscalYearId` سال جدید، تاریخ = روز اول سال، SourceModule=Accounting، IdempotencyKey=`opening:{company}:{fy}`).

## ۲. اسناد اصلاحی (Adjustment Engine)

`AdjustmentVoucherService.CreateAdjustmentVoucherAsync` — اصلاح اشتباه ثبت/طبقه‌بندی/مبلغ/تفصیلی.

- سند جدید `DocumentType.Adjustment` با `ReferenceDocumentId` متصل به سند مرجع قطعی؛ **سند اصلی هرگز بازنویسی نمی‌شود** (قطعی‌ها تغییرناپذیرند).
- علت اصلاح الزامی (`AdjustmentReasonRequired`) و در سابقه ممیزی سند ثبت می‌شود.
- فقط مرجع قطعی قابل اصلاح است؛ تاریخ اصلاح باید در سال/دوره باز باشد (اصلاح در سال بسته ممنوع).
- تراز اصلاح توسط موتور Posting کنترل می‌شود؛ اصلاح تفصیلی با ابعاد ردیف (PROMPT 06) انجام می‌شود.

## ۳. معکوس‌سازی (Reversal Engine)

- سند معکوس با بدهکار/بستانکار جابه‌جا، ابعاد کامل منعکس (PROMPT 06) و اتصال دوسویه با `ReversedDocumentId`.
- **ضدتکرار Idempotent**: معکوس‌سازی دوباره سند معکوس‌شده همان سند معکوس قبلی را با `WasAlreadyReversed=true` برمی‌گرداند — سند دومی ساخته نمی‌شود.
- علت اصلاح در شرح و ممیزی ثبت می‌شود (endpoint جدید علت را اجباری می‌کند).
- مجوز: `accounting:reversals:write` در API + ماشین وضعیت دامنه (فقط Posted قابل معکوس).

## ۴. گردش‌کار اختتامیه (Closing Workflow)

`FiscalClosingWorkflowService.RunClosingAsync` — مرحله‌ای، قابل پیگیری و قابل بازیابی. موجودیت `FiscalYearClosing` + جدول مراحل `FiscalYearClosingStep`.

وضعیت‌ها: `NotStarted → Validating → ReadyToClose → Closing → Closed` و `Failed`.

| مرحله | خروجی ثبت‌شده |
| :--- | :--- |
| ۱. بررسی وضعیت دوره‌ها | تعداد دوره‌ها به تفکیک وضعیت؛ دوره Draft = مردود |
| ۲. بررسی اسناد Draft/Pending | شمارش اسناد ثبت‌نشده؛ وجودشان مردود می‌کند |
| ۳. کنترل تراز دفتر کل | جمع بدهکار/بستانکار و تعداد سطر؛ عدم تراز مردود |
| ۴. محاسبه درآمد/هزینه/سود | مبالغ علامت‌دار + اعتبارسنجی حساب مقصد (Equity) |
| ۵. سند اختتامیه | صفر کردن هر درآمد (بدهکار) و هر هزینه (بستانکار) + انتقال نتیجه به حساب مقصد؛ بدون درآمد/هزینه، سندی لازم نیست |
| ۶. بستن سال | بسته شدن همه دوره‌ها + ثبت ClosedAt/By |

- **بازیابی**: شکست در هر مرحله `Failed` + علت + مراحل انجام‌شده را حفظ می‌کند؛ اجرای مجدد پس از رفع مشکل موفق است. اگر اجرای قبلی سند اختتامیه را قطعی کرده و در بستن سال شکست خورده باشد، اجرای بعدی «فقط» بستن سال را ادامه می‌دهد (بدون سند تکراری).
- **ضدتکرار**: سال بسته یا گردش‌کار Closed → `ClosingAlreadyDone`.
- دوره پایان سال برای ثبت سند اختتامیه باید باز باشد (`ClosingPeriodNotOpen`).

## ۵. بازگشایی (Reopening)

`FiscalYearReopeningService.ReopenAsync` — فقط با فرایند رسمی:

- رکورد کامل `FiscalYearReopening`: درخواست‌دهنده، **تأییدکننده متمایز** (`ReopeningSelfApproval`)، دلیل الزامی، زمان، اثر بر افتتاحیه سال بعد.
- **کنترل افتتاحیه سال بعد**: اگر سال بعد افتتاحیه دارد → رد (`ReopeningBlockedByNextOpening`)؛ اصلاح باید در اولین سال باز با تعدیل/معکوس انجام شود. اگر افتتاحیه ندارد → ثبت هشدار اثر در رکورد.
- بازگشایی، دوره‌های بسته را باز نمی‌کند (پذیرش سند نیازمند بازگشایی صریح هر دوره است) و اسناد قطعی همچنان تغییرناپذیرند.
- مجوز API: `accounting:fiscalyear:reopen`.

## ۶. API خلاصه (`api/v1/accounting/fiscal-operations`)

| عملیات | مسیر | Permission |
| :--- | :--- | :--- |
| ایجاد افتتاحیه | `POST /opening-vouchers` | `accounting:opening:write` |
| مشاهده افتتاحیه | `GET /opening-vouchers/{fyId}` | `accounting:opening:read` |
| سند اصلاحی | `POST /adjustment-vouchers` | `accounting:adjustments:write` |
| معکوس با علت | `POST /reversals` | `accounting:reversals:write` |
| اجرای اختتامیه | `POST /closing-workflows` | `accounting:fiscalyear:close` |
| وضعیت اختتامیه | `GET /closing-workflows/{fyId}` | `accounting:fiscalyear:read` |
| بازگشایی | `POST /fiscal-years/{id}/reopen` | `accounting:fiscalyear:reopen` |
| سابقه بازگشایی | `GET /fiscal-years/{id}/reopenings` | `accounting:fiscalyear:read` |

## ۷. محدودیت‌های رعایت‌شده

- سند قطعی قبلی بازنویسی نمی‌شود (اصلاح فقط سند جدید متصل به مرجع).
- اختتامیه یک Update ساده نیست: گردش‌کار ۶ مرحله‌ای با سابقه، سند اختتامیه مستقل، و بستن سال در انتهای فرایند.
- همه عملیات مالی با ممیزی: DocumentAuditLog برای افتتاحیه/اصلاحی/معکوس/اختتامیه + جدول مراحل + رکورد بازگشایی.
- سال بسته بدون کنترل قابل استفاده نیست: پذیرش سند رد می‌شود، اسناد قبلی تغییرناپذیرند، بازگشایی رسمی با کنترل اثر.

## ۸. تست‌ها

- `FiscalYearClosingIntegrationTests` (۱۵ تست): هر ۱۳ سناریوی اجباری + انعکاس ابعاد در افتتاحیه + سابقه ممیزی کامل چرخه.
- `FinancialBalancePropertyTests`: ۲۰ seed تصادفی از دیتاست متوازن؛ ناورداها: تراز GL در هر مقطع، صفر شدن موقت‌ها، سود/زیان مستقل = مانده مقصد، ادامه مانده دائمی در افتتاحیه، عدم تغییر دائمی‌ها با اختتامیه.
- Migration: `AddFiscalClosingWorkflow` (۳ جدول + ستون ReferenceDocumentId).
