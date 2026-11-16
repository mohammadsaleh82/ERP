# راهنمای اجرای آزمون‌های خودکار و کنترل کیفیت (Testing Guide)

این سند انواع آزمون‌ها، ابزارها و دستورالعمل‌های تست را در سیستم ERP تشریح می‌کند.

---

## ۱. هرم آزمون‌ها در سیستم ERP

```text
               /\
              /  \     Security & Integration Tests (21 Tests)
             /    \    - Multi-Tenant Isolation
            /      \   - Token Rotation & Compromise Detection
           /        \  - RFC 7807 ProblemDetails & Membership Guards
          /----------\
         /            \ Architecture Tests (17 Tests)
        /              \ - NetArchTest Rules
       /                \ - Zero Cross-Module Dependencies
      /------------------\
     /                    \ Unit Tests (2 Tests)
    /                      \ - Core BuildingBlocks & Result Types
   /------------------------\
```

---

## ۲. دسته‌بندی پروژه‌های آزمون

1. **[ERP.ArchitectureTests](file:///d:/ffff/MyProject/ERP/tests/ERP.ArchitectureTests/):**
   - سنجش قواعد معماری Clean و Modular Monolith به صورت خودکار با NetArchTest.
   - تضمین عدم رفرنس ماژول به ماژول دیگر.
   - تضمین عدم وابستگی لایه Domain به Infrastructure یا کتابخانه‌های دیتابیس.
   - تضمین استقلال کامل BuildingBlocks.

2. **[ERP.SecurityTests](file:///d:/ffff/MyProject/ERP/tests/ERP.SecurityTests/):**
   - سنجش ایزولاسیون امنیتی مستاجران (Scenarios A, B, C, D, E).
   - سنجش چرخش Refresh Token و کشف استفاده مجدد از توکن‌های باطل‌شده (Token Reuse Detection).
   - آزمون بازگرداندن خطای `403 Forbidden` در صورت ارسال شناسه شرکت نامعتبر توسط کاربر احراز هویت‌شده.
   - آزمون اعتبارسنجی سیستم مجوزدهی پویا `[HasPermission]`.

3. **[ERP.IntegrationTests](file:///d:/ffff/MyProject/ERP/tests/ERP.IntegrationTests/):**
   - اثبات ایزولاسیون کوئری و تفکیک ۲ مستاجر مستقل با پایگاه‌داده SQLite In-Memory.
   - آزمون سیاست‌های داده‌ای (UTC DateTime، دقت اعشار 28,4، ره‌گیری خودکار زمان).
   - آزمون خطاهای استاندارد RFC 7807 ProblemDetails در محیط‌های Development و Production.

4. **[ERP.UnitTests](file:///d:/ffff/MyProject/ERP/tests/ERP.UnitTests/):**
   - آزمون واحد بر روی المان‌های پایه‌ای BuildingBlocks (الگوی Result و Error handling).

---

## ۳. اجرای دستورات آزمون

```bash
# اجرای کل سوئیت آزمون‌ها
dotnet test ERP.sln

# اجرای اختصاصی آزمون‌های معماری
dotnet test tests/ERP.ArchitectureTests/

# اجرای اختصاصی آزمون‌های امنیت
dotnet test tests/ERP.SecurityTests/

# اجرای اختصاصی آزمون‌های یکپارچگی
dotnet test tests/ERP.IntegrationTests/
```

---

## ۴. اجرای تست‌های هم‌زمانی موتور سند روی PostgreSQL واقعی (PROMPT 04)

تست‌های هم‌زمانی Posting (`VoucherConcurrencyPostgresTests`) برخلاف بقیه تست‌ها روی **PostgreSQL واقعی** اجرا می‌شوند و از SQLite استفاده نمی‌کنند:

```bash
# روش ۱: استفاده از PostgreSQL کانتینری پروژه
docker compose up -d postgres

# روش ۲: رشته اتصال سفارشی از طریق متغیر محیطی
export ERP_TEST_POSTGRES="Host=localhost;Port=5432;Username=erp_user;Password=***;Database=postgres"

# اجرای فقط تست‌های هم‌زمانی
dotnet test tests/ERP.IntegrationTests/ --filter "FullyQualifiedName~VoucherConcurrencyPostgresTests"
```

- هر تست یک پایگاه‌داده ایزوله با نام یکتا می‌سازد و در پایان پاک می‌کند.
- پیش‌فرض (در نبود متغیر محیطی): `Host=localhost;Port=5433;Username=erp_user;Password=erp_test_password`.
- در نبود سرور PostgreSQL، این تست‌ها با پیام توضیحی در خروجی رد می‌شوند تا بقیه سوئیت سبز بماند.

پوشش سناریوهای حیاتی موتور سند (۱۵ سناریوی PROMPT 04) در این فایل‌ها است:
- `tests/ERP.UnitTests/DocumentWorkflowStateTests.cs` — ماشین وضعیت و سیاست‌های تأیید
- `tests/ERP.UnitTests/VoucherDoubleEntryRulesTests.cs` — قوانین دوبل در سطح دامنه
- `tests/ERP.IntegrationTests/VoucherPostingIntegrationTests.cs` — سناریوهای ۱ تا ۹ و ۱۱ تا ۱۵ روی پایگاه‌داده
- `tests/ERP.IntegrationTests/VoucherConcurrencyPostgresTests.cs` — سناریو ۱۰ (هم‌زمانی) روی PostgreSQL واقعی
- `tests/ERP.SecurityTests/VoucherSecurityTests.cs` — گاردهای امنیتی سند و مجوزها
- `tests/ERP.IntegrationTests/LedgerReportingIntegrationTests.cs` — ۱۲ سناریوی دفتر روزنامه/کل/معین و موتور مانده (PROMPT 05)
- `tests/ERP.IntegrationTests/LedgerPerformanceTests.cs` — تست کارایی با دیتاست ۱۰۰۰ سند / ۲۰۰۰ سطر دفتر کل
- `tests/ERP.UnitTests/DimensionValidationTests.cs` — موتور قواعد ابعاد مالی خالص (PROMPT 06)
- `tests/ERP.IntegrationTests/FinancialDimensionIntegrationTests.cs` — ۱۲ سناریوی تفصیلی شناور و ابعاد مالی
- `tests/ERP.IntegrationTests/FiscalYearClosingIntegrationTests.cs` — ۱۵ سناریوی افتتاحیه/اصلاح/معکوس/اختتامیه/بازگشایی
- `tests/ERP.IntegrationTests/FinancialBalancePropertyTests.cs` — ناورداهای حفظ تراز در چرخه کامل روی ۲۰ seed تصادفی
