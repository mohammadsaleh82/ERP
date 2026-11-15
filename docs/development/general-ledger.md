# دفتر روزنامه، دفتر کل، دفتر معین و موتور مانده (Journal, General Ledger, Subsidiary Ledger & Balance Engine)

مرجع کامل معماری گزارش‌های دفتر (PROMPT 05). بنیاد گردش مالی از موتور سند (PROMPT 04) می‌آید: هر سند قطعی در همان تراکنش Posting، به ازای هر ردیف، یک سطر `GeneralLedgerEntry` با ارجاع کامل ایجاد می‌کند.

---

## ۱. ساختار گردش مالی و ردیابی (Posted Ledger)

هر سطر دفتر کل (`acc_gl_entries`) به تمام موارد زیر قابل ردیابی است:

| ستون | توضیح |
| :--- | :--- |
| `TenantId / CompanyId / BranchId` | مالکیت چندمستاجره و شعبه |
| `FiscalYearId / FiscalPeriodId` | سال و دوره مالی |
| `DocumentId / DocumentLineId / DocumentNumber / LineNumber` | ارجاع به سند و ردیف مبدأ |
| `AccountId` | حساب گردش |
| `EntryDate` | تاریخ مالی |
| `DebitAmount / CreditAmount` | بدهکار / بستانکار (decimal 28,4) |
| `CurrencyCode / ForeignCurrencyCode / ExchangeRate` | ارز پایه و ارزی |
| `SourceModule / SourceDocumentId` | منشأ سیستمی |

### تغییرناپذیری Posted Ledger

سطرهای دفتر کل پس از ثبت، **هرگز** قابل UPDATE یا DELETE نیستند:

- گارد در `AccountingDbContext.SaveChangesAsync`: هر تلاش برای ویرایش/حذف سطر دفتر کل با `Accounting.PostedLedgerImmutable` مسدود می‌شود.
- اصلاح تنها با عملیات معتبر «سند معکوس (Storno)» انجام می‌شود که سطرهای جدید `IsReversal=true` می‌سازد.
- گارد پایگاه‌داده‌ای نیز موازی عمل می‌کند: ایندکس یکتای `(tenant, company, document, document_line)` از نوشتن دوباره جلوگیری می‌کند.

## ۲. Consistency: سند و گردش هم‌زمان می‌نویسند

- سطرهای دفتر کل در **همان تراکنش اتمیک Posting** نوشته می‌شوند؛ سند قطعیِ بدون گردش ممکن نیست.
- `GlConsistencyService.VerifyAsync` در هر لحظه تطبیق می‌دهد: تعداد و جمع گردش هر سند قطعی باید دقیقاً برابر ردیف‌ها و جمع سند باشد؛ هیچ گردش یتیم (بدون سند قطعی) نیز مجاز نیست.
- Endpoint: `GET /api/v1/accounting/ledger/consistency`.

## ۳. دفتر روزنامه (Journal)

`GET /api/v1/accounting/ledger/journal` — اسناد قطعی به ترتیب **تاریخ و شماره سند** با:

- فیلترها: بازه تاریخی، شماره سند، حساب، شعبه، سال مالی
- ردیف‌ها: سند با شرح، سطرهای حساب (کد/نام/شرح)، بدهکار، بستانکار
- **جمع روزانه**: گروه‌بندی بر اساس تاریخ روی کل بازه فیلترشده
- **جمع دوره‌ای**: دو تجمیع مستقل روی کل بازه (مستقل از صفحه جاری)
- صفحه‌بندی واقعی روی اسناد (`PageNumber/PageSize` حداکثر ۱۰۰)

## ۴. دفتر کل (General Ledger)

- `GET /ledger/general-ledger` — خلاصه گردش/مانده حساب‌ها با صفحه‌بندی واقعی روی حساب‌های قابل ثبت (گزینه `IncludeNonPostingAccounts`)
- `GET /ledger/general-ledger/accounts/{id}/transactions` — گردش سطر به سطر یک حساب با مانده اول/پایان دوره
- فیلترها: حساب، بازه تاریخ، سال مالی، شعبه

هر ردیف خلاصه شامل: OpeningBalance (علامت‌دار)، PeriodDebit، PeriodCredit، ClosingBalance، جهت مانده، مقدار نمایشی و پرچم غیرعادی بودن.

## ۵. دفتر معین (Subsidiary Ledger)

`GET /ledger/subsidiary-ledger?parentAccountId=...` — گردش حساب‌های زیرمجموعه یک حساب کل:

- درخت حساب‌ها یک‌بار بارگذاری و فرزندان به‌صورت BFS استخراج می‌شوند (بدون Recursion در SQL، بدون N+1).
- **عدم شمارش دوباره**: سطرهای دفتر کل فقط روی حساب‌های قابل ثبت (معین/تفصیلی) وجود دارند؛ حساب‌های گروه و کل هرگز سند نمی‌پذیرند. بنابراین هر سطر دقیقاً یک‌بار و تنها در حساب مبدأ شمرده می‌شود. جمع ردیف‌های گزارش در تست‌ها با تجمیع مستقیم GL مجموعه فرزندان مقایسه می‌شود.
- حساب‌های بدون گردش در گزارش ظاهر نمی‌شوند؛ مانده کل مجموعه = جمع مانده فرزندان.

## ۶. موتور مانده (Balance Engine)

`GET /ledger/balances` — محاسبه مستقیماً از منبع اصلی (acc_gl_entries):

```text
OpeningBalance  = Σ(بدهکار − بستانکار) قبل از شروع بازه
PeriodDebit     = Σ بدهکار داخل بازه
PeriodCredit    = Σ بستانکار داخل بازه
ClosingBalance  = Opening + PeriodDebit − PeriodCredit   (علامت‌دار)
```

- اسناد Draft هیچ سطر دفتر کل ندارند؛ بنابراین **هیچ‌گاه** در مانده اثر نمی‌گذارند.
- محدوده: با `PeriodId` یا `FiscalYearId` مانده اول دوره از ابتدای سال محاسبه می‌شود؛ سال مالی مشخص یعنی کل محاسبات به گردش همان سال محدود است (مانده‌های افتتاحیه به‌صورت سند افتتاحیه در خود سال ثبت می‌شوند).

### سیاست نمایش مانده منفی (`BalanceCalculator`)

قرارداد علامت‌گذاری:

- مانده علامت‌دار = بدهکار − بستانکار؛ **مثبت = مانده بدهکار، منفی = مانده بستانکار**؛ علامت هرگز حذف نمی‌شود.
- مقدار نمایشی (`DisplayBalance`) همیشه قدرمطلق مانده است.
- جهت نمایش (`ClosingDirection`) صریحاً Debtor/Creditor/None اعلام می‌شود.
- مانده **خلاف ماهیت عادی حساب** (مثلاً مانده بدهکار روی حساب با ماهیت بستانکار) با `IsAbnormalClosingBalance=true` پرچم می‌خورد — یعنی «مانده منفی/معکوس» با سیاست شفاف نمایش داده می‌شود، نه با علامت گم‌شده.

این منطق Pure است و در `ERP.UnitTests/BalanceDirectionTests` پوشش داده شده.

## ۷. کارایی (Performance)

- **ایندکس‌ها**: `(Tenant, Company, Account, EntryDate)` برای مانده، `(Tenant, Company, EntryDate, DocumentNumber)` برای ترتیب دفتر روزنامه، `(Tenant, Company, Branch, EntryDate)` برای فیلتر شعبه، `(Tenant, Company, DocumentId)` و `(Tenant, Company, FY, Period)` برای پیوند سند.
- **جدا بودن Query Side**: `LedgerQueryService` / `BalanceEngine` فقط خواندنی هستند (`AsNoTracking`) و از سرویس‌های نوشتن جدا شده‌اند؛ منطق مانده در `AccountingDbContext` ممنوعیت نوشتن ندارد اما مسیر Query هیچ SaveChanges نمی‌کند.
- **بدون N+1**: مشخصات حساب‌ها یک‌بار و دسته‌ای بارگذاری می‌شود؛ سطرهای دفتر روزنامه در یک کوئری.
- **پروجکشن**: فقط ستون‌های موردنیاز با `.Select(...)` از دیتابیس می‌آیند.
- **Materialized Read Model**: `AccountBalanceSnapshot` (`acc_account_balance_snapshots`) — یک ردیف به ازای هر (شرکت، دوره، حساب) با گردش دوره؛ به‌صورت اتمیک در همان تراکنش Posting توسط `BalanceSnapshotService.ApplyAsync` انبارش می‌شود (با Retry در برخورد هم‌زمانی). مسیر خواندن سریع: `GET /ledger/balances/snapshot`.
- **قاعده طلایی**: اسنپ‌شات هرگز جایگزین منبع اصلی نمی‌شود؛ اعتبار آن با Reconciliation قابل اثبات است:
  - `GET /ledger/balances/reconcile` → مقایسه اسنپ‌شات با تجمیع زنده GL (تفاوت‌ها با جزئیات ردیفی)
  - `POST /ledger/balances/snapshots/rebuild` → بازسازی کامل از GL (نیازمند مجوز `accounting:ledger:rebuild`)
- تست کارایی: `LedgerPerformanceTests` — ۱۰۰۰ سند / ۲۰۰۰ سطر دفتر کل؛ صحت تجمیع‌ها (تطابق دقیق با اسناد قطعی) + زمان اجرای همه گزارش‌ها در سقف مجاز.

## ۸. API خلاصه

| عملیات | مسیر | Permission |
| :--- | :--- | :--- |
| دفتر روزنامه | `GET /api/v1/accounting/ledger/journal` | `accounting:ledger:read` |
| دفتر کل (خلاصه) | `GET /ledger/general-ledger` | `accounting:ledger:read` |
| گردش تفصیلی حساب | `GET /ledger/general-ledger/accounts/{id}/transactions` | `accounting:ledger:read` |
| دفتر معین | `GET /ledger/subsidiary-ledger` | `accounting:ledger:read` |
| مانده‌ها (زنده) | `GET /ledger/balances` | `accounting:ledger:read` |
| مانده‌ها (اسنپ‌شات) | `GET /ledger/balances/snapshot` | `accounting:ledger:read` |
| مطابقت‌سنجی اسنپ‌شات | `GET /ledger/balances/reconcile` | `accounting:ledger:read` |
| بازسازی اسنپ‌شات | `POST /ledger/balances/snapshots/rebuild` | `accounting:ledger:rebuild` |
| تطبیق اسناد با گردش | `GET /ledger/consistency` | `accounting:ledger:read` |

همه گزارش‌ها چندمستاجره‌اند (فیلتر Tenant/Company از زمینه درخواست) و فقط اسناد قطعی را ملاک قرار می‌دهند.
